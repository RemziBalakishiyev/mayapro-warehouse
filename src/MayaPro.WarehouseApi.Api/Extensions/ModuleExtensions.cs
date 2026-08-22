using System.Reflection;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace MayaPro.WarehouseApi.Api.Extensions;

/// <summary>
/// Discovers every <see cref="IModule"/> implementation across the loaded assemblies and wires it
/// into the host: services, then endpoints, then migrations. This is the whole modular-monolith glue —
/// adding a new module means implementing <see cref="IModule"/>, nothing here changes.
/// </summary>
public static class ModuleExtensions
{
    private static readonly List<IModule> DiscoveredModules = new();

    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration)
    {
        foreach (IModule module in DiscoverModules())
        {
            module.RegisterServices(services, configuration);
            services.AddSingleton(module);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        foreach (IModule module in DiscoveredModules)
            module.MapEndpoints(endpoints);

        return endpoints;
    }

    public static async Task MigrateModulesAsync(this IServiceProvider services)
    {
        foreach (IModule module in DiscoveredModules)
            await MigrateModuleWithRetryAsync(services, module);
    }

    /// <summary>
    /// Runs a module's migrate + seed with a few retries and an increasing delay. A freshly started (cold)
    /// SQL Server can be slow to accept the very first connections, so the first attempt may hit a connection
    /// timeout that a short wait resolves. This is startup-only; it is NOT EnableRetryOnFailure (that would
    /// clash with the hand-rolled BeginTransaction flow) — each attempt uses a fresh scope so a failed
    /// attempt never reuses a half-open connection.
    /// </summary>
    /// <remarks>
    /// BE#50: the <c>NormalizePhoneNumbers</c> data migrations (and any future migration that does the same)
    /// report their result via <c>RAISERROR(..., 0, 1) WITH NOWAIT</c>, which only reaches
    /// <see cref="SqlConnection.InfoMessage"/> — EF Core never forwards it to <see cref="ILogger"/>. Nothing
    /// listened for that event in the real start-up path, so those lines never reached the app's own log
    /// output. The scope's shared connection (the same one every module <c>DbContext</c> in this scope writes
    /// through, via <see cref="IDbConnectionFactory"/>) is listened on for the duration of this one migration
    /// run and unsubscribed immediately after, so nothing outlives the run.
    /// </remarks>
    internal static async Task MigrateModuleWithRetryAsync(IServiceProvider services, IModule module)
    {
        const int maxAttempts = 3;
        ILogger logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("MayaPro.WarehouseApi.Api.Extensions.ModuleExtensions");

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await using AsyncServiceScope scope = services.CreateAsyncScope();

                var connection = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>()
                    .GetConnection() as SqlConnection;
                using IDisposable? subscription = connection is null
                    ? null
                    : ListenForMigrationLog(connection, logger);

                await module.MigrateAsync(scope.ServiceProvider);
                return;
            }
            catch when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
        }
    }

    /// <summary>
    /// BE#50 — subscribes <paramref name="logger"/> to <paramref name="connection"/>'s
    /// <see cref="SqlConnection.InfoMessage"/> event and returns an <see cref="IDisposable"/> that removes the
    /// handler again. A line reporting one or more unreadable values (<c>cevrile bilmedi: M</c>, M &gt; 0) is
    /// logged as a <see cref="LogLevel.Warning"/> — an operator otherwise has no way to know a row was left
    /// untouched; every other line is <see cref="LogLevel.Information"/>.
    /// </summary>
    internal static IDisposable ListenForMigrationLog(SqlConnection connection, ILogger logger)
    {
        SqlInfoMessageEventHandler handler = (_, e) =>
        {
            foreach (SqlError error in e.Errors)
            {
                if (TryGetUnconvertibleCount(error.Message, out int unconvertible) && unconvertible > 0)
                    logger.LogWarning("{MigrationMessage}", error.Message);
                else
                    logger.LogInformation("{MigrationMessage}", error.Message);
            }
        };

        connection.InfoMessage += handler;
        return new MigrationLogSubscription(connection, handler);
    }

    /// <summary>
    /// Pulls the <c>cevrile bilmedi: M</c> count out of a migration's result line (see the
    /// <c>NormalizePhoneNumbers</c> migrations, BE#46). Returns <see langword="false"/> for any line that
    /// does not carry that marker, so unrelated <c>InfoMessage</c> lines still get logged (as Information)
    /// rather than dropped.
    /// </summary>
    internal static bool TryGetUnconvertibleCount(string message, out int count)
    {
        const string marker = "cevrile bilmedi: ";

        count = 0;
        int markerIndex = message.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return false;

        string digits = new(message[(markerIndex + marker.Length)..].TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out count);
    }

    private sealed class MigrationLogSubscription(SqlConnection connection, SqlInfoMessageEventHandler handler)
        : IDisposable
    {
        public void Dispose() => connection.InfoMessage -= handler;
    }

    /// <summary>
    /// Instantiates each concrete <see cref="IModule"/> exactly once. Referenced module assemblies are
    /// force-loaded first, since they may not yet be in the AppDomain if no type has been touched.
    /// </summary>
    private static IReadOnlyList<IModule> DiscoverModules()
    {
        if (DiscoveredModules.Count > 0)
            return DiscoveredModules;

        LoadReferencedAssemblies();

        IEnumerable<IModule> modules = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(SafeGetTypes)
            .Where(type => typeof(IModule).IsAssignableFrom(type)
                           && type is { IsInterface: false, IsAbstract: false })
            .Select(type => (IModule)Activator.CreateInstance(type)!)
            .OrderBy(module => module.Name, StringComparer.Ordinal);

        DiscoveredModules.AddRange(modules);
        return DiscoveredModules;
    }

    /// <summary>
    /// Loads every <c>MayaPro.WarehouseApi.*</c> assembly sitting next to the host. Module assemblies
    /// are referenced but never touched directly by the host, so the compiler trims them from metadata
    /// and they are absent from the AppDomain until loaded explicitly. Scanning the deployment folder
    /// is host-agnostic — it works identically under the API host and under WebApplicationFactory.
    /// </summary>
    private static void LoadReferencedAssemblies()
    {
        var loaded = new HashSet<string>(
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Select(a => a.GetName().Name!),
            StringComparer.Ordinal);

        foreach (string path in Directory.EnumerateFiles(
                     AppContext.BaseDirectory, "MayaPro.WarehouseApi.*.dll"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!loaded.Add(name))
                continue;

            try
            {
                Assembly.Load(new AssemblyName(name));
            }
            catch (Exception)
            {
                // Ignore assemblies that cannot be loaded — they cannot host a module anyway.
            }
        }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
