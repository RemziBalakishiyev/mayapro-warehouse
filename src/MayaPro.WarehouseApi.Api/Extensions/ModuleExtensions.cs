using System.Reflection;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;

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
    private static async Task MigrateModuleWithRetryAsync(IServiceProvider services, IModule module)
    {
        const int maxAttempts = 3;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await using AsyncServiceScope scope = services.CreateAsyncScope();
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
