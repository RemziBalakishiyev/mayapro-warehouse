using System.Data;
using System.Reflection;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
// System.Reflection also declares a ModuleExtensions static class, so the host's is aliased explicitly.
using ModuleExtensions = MayaPro.WarehouseApi.Api.Extensions.ModuleExtensions;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// BE#50 — before this, the <c>NormalizePhoneNumbers</c> migrations' <c>RAISERROR(..., 0, 1) WITH NOWAIT</c>
/// result line only ever reached <see cref="SqlConnection.InfoMessage"/>; the real start-up path
/// (<c>ModuleExtensions.MigrateModuleWithRetryAsync</c>) had no subscriber, so it never reached the app's own
/// log output. These tests exercise the internal helpers directly against a real SQL Server connection — the
/// only way to prove what <see cref="SqlConnection.InfoMessage"/> actually delivers.
/// </summary>
public sealed class ModuleExtensionsMigrationLogTests
{
    private const string ConnectionString =
        "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;" +
        "MultipleActiveResultSets=True";

    // ---------------------------------------------------------------- TryGetUnconvertibleCount (AC-2, pure)

    [Theory]
    [InlineData("[BE#46] customers.Customers.Phone - normallasdirildi: 3, cevrile bilmedi: 3", true, 3)]
    [InlineData("[BE#46] tenancy.Tenants.Phone - normallasdirildi: 2, cevrile bilmedi: 0", true, 0)]
    [InlineData("some unrelated info message", false, 0)]
    public void TryGetUnconvertibleCount_Parses_The_Marker(string message, bool expectedSuccess, int expectedCount)
    {
        bool success = ModuleExtensions.TryGetUnconvertibleCount(message, out int count);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedCount, count);
    }

    // ---------------------------------------------------------------- ListenForMigrationLog (AC-1, AC-2, AC-3)

    /// <summary>AC-2: an unconvertible count above zero is logged as a Warning.</summary>
    [Fact]
    public async Task ListenForMigrationLog_Logs_Warning_When_Unconvertible_Count_Is_Positive()
    {
        var logger = new FakeLogger();
        await using SqlConnection connection = await OpenConnectionAsync();

        using (ModuleExtensions.ListenForMigrationLog(connection, logger))
        {
            await RaiseAsync(connection,
                "[BE#50] test.Table.Phone - normallasdirildi: 1, cevrile bilmedi: 2");
        }

        (LogLevel Level, string Message) entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("cevrile bilmedi: 2", entry.Message);
    }

    /// <summary>AC-1: a clean result (nothing unconvertible) still reaches the log, just at Information.</summary>
    [Fact]
    public async Task ListenForMigrationLog_Logs_Information_When_Unconvertible_Count_Is_Zero()
    {
        var logger = new FakeLogger();
        await using SqlConnection connection = await OpenConnectionAsync();

        using (ModuleExtensions.ListenForMigrationLog(connection, logger))
        {
            await RaiseAsync(connection,
                "[BE#50] test.Table.Phone - normallasdirildi: 3, cevrile bilmedi: 0");
        }

        (LogLevel Level, string Message) entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    /// <summary>
    /// AC-3: disposing the subscription actually removes the handler — a second message on the very same
    /// still-open connection, raised after disposal, must not add a second log entry. Proving this against
    /// scope-disposal side effects would be meaningless (the connection dies with the scope either way), so
    /// this keeps the connection alive across both calls to isolate the unsubscribe itself.
    /// </summary>
    [Fact]
    public async Task ListenForMigrationLog_Stops_Logging_Once_Disposed()
    {
        var logger = new FakeLogger();
        await using SqlConnection connection = await OpenConnectionAsync();

        IDisposable subscription = ModuleExtensions.ListenForMigrationLog(connection, logger);
        await RaiseAsync(connection, "[BE#50] test.Table.Phone - normallasdirildi: 1, cevrile bilmedi: 0");
        Assert.Single(logger.Entries);

        subscription.Dispose();
        await RaiseAsync(connection, "[BE#50] test.Table.Phone - normallasdirildi: 1, cevrile bilmedi: 0");

        // Still exactly one — the second RAISERROR reached nobody.
        Assert.Single(logger.Entries);
    }

    // ---------------------------------------------------------------- MigrateModuleWithRetryAsync (AC-1, AC-2)

    /// <summary>AC-1 end to end: the real start-up entry point now forwards a module's migration result.</summary>
    [Fact]
    public async Task MigrateModuleWithRetryAsync_Forwards_The_Migration_Result_To_ILogger()
    {
        var logger = new FakeLogger();
        await using ServiceProvider provider = BuildProvider(logger);
        IModule module = FakeMigrationModule.Create(
            "RAISERROR(N'[BE#50] test.Table.Phone - normallasdirildi: 5, cevrile bilmedi: 0', 0, 1) WITH NOWAIT;");

        await ModuleExtensions.MigrateModuleWithRetryAsync(provider, module);

        (LogLevel Level, string Message) entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("normallasdirildi: 5", entry.Message);
    }

    /// <summary>AC-2 end to end: an unconvertible count above zero surfaces as a Warning through the real
    /// start-up entry point, not just through the lower-level helper.</summary>
    [Fact]
    public async Task MigrateModuleWithRetryAsync_Logs_A_Warning_When_The_Module_Reports_Unconvertible_Rows()
    {
        var logger = new FakeLogger();
        await using ServiceProvider provider = BuildProvider(logger);
        IModule module = FakeMigrationModule.Create(
            "RAISERROR(N'[BE#50] identity.Users.Phone - normallasdirildi: 2, cevrile bilmedi: 1', 0, 1) WITH NOWAIT;");

        await ModuleExtensions.MigrateModuleWithRetryAsync(provider, module);

        (LogLevel Level, string Message) entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("cevrile bilmedi: 1", entry.Message);
    }

    // ---------------------------------------------------------------- plumbing

    private static async Task<SqlConnection> OpenConnectionAsync()
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task RaiseAsync(SqlConnection connection, string message)
    {
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = $"RAISERROR(N'{message.Replace("'", "''")}', 0, 1) WITH NOWAIT;";
        await command.ExecuteNonQueryAsync();
    }

    private static ServiceProvider BuildProvider(ILogger logger)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(new FakeLoggerFactory(logger));
        services.AddScoped<IDbConnectionFactory, MasterDbConnectionFactory>();
        return services.BuildServiceProvider();
    }

    /// <summary>Mirrors <c>SqlConnectionFactory</c>'s shape (one lazily-created connection per scope) without
    /// depending on <c>ConnectionStrings:Default</c> configuration.</summary>
    private sealed class MasterDbConnectionFactory : IDbConnectionFactory, IAsyncDisposable
    {
        private SqlConnection? _connection;

        public System.Data.Common.DbConnection GetConnection() => _connection ??= new SqlConnection(ConnectionString);

        public async ValueTask DisposeAsync()
        {
            if (_connection is not null)
                await _connection.DisposeAsync();
        }
    }

    /// <summary>
    /// A fake <see cref="IModule"/> whose only job is to open the scope's shared connection and raise one
    /// info message — standing in for a real EF Core migration's <c>RAISERROR</c> result line.
    /// <para>
    /// Built via <see cref="DispatchProxy"/> rather than a plain class implementing <see cref="IModule"/>:
    /// <c>ModuleExtensions.DiscoverModules()</c> reflects over every non-dynamic loaded assembly — including
    /// this test assembly — for concrete <see cref="IModule"/> types and instantiates whatever it finds via
    /// <c>Activator.CreateInstance</c>. A plain test-double class would be discovered as a "real" module by
    /// every other test in this project that boots the host (<c>WarehouseApiFactory</c>), breaking their
    /// start-up. <see cref="DispatchProxy"/> generates its proxy type in a dynamic assembly, which
    /// <c>DiscoverModules()</c> explicitly skips (<c>!assembly.IsDynamic</c>), so it stays invisible to
    /// module discovery while still satisfying <see cref="IModule"/> for this call.
    /// </para>
    /// </summary>
    public class FakeMigrationModule : DispatchProxy
    {
        private string _sql = string.Empty;

        public static IModule Create(string sql)
        {
            object proxy = Create<IModule, FakeMigrationModule>();
            ((FakeMigrationModule)proxy)._sql = sql;
            return (IModule)proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name switch
            {
                nameof(IModule.MigrateAsync) => MigrateAsync((IServiceProvider)args![0]!),
                _ => throw new NotSupportedException(
                    $"{targetMethod?.Name} is not used by ModuleExtensionsMigrationLogTests's fake module.")
            };

        private async Task MigrateAsync(IServiceProvider services)
        {
            var connection = (SqlConnection)services.GetRequiredService<IDbConnectionFactory>().GetConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sql;
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>Hands the same <see cref="ILogger"/> back for any category — the tests only need one sink.</summary>
    private sealed class FakeLoggerFactory(ILogger logger) : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => logger;

        public void Dispose()
        {
        }
    }

    /// <summary>Captures log entries without a mocking library, mirroring the module test projects' double of
    /// the same name (this one implements the non-generic <see cref="ILogger"/>, since
    /// <c>ILoggerFactory.CreateLogger(string)</c> does not return an <c>ILogger&lt;T&gt;</c>).</summary>
    private sealed class FakeLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
