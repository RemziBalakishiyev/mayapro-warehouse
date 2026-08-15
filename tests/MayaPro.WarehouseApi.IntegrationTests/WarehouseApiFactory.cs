using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// Boots the real host against a dedicated test database (<c>MayaProWarehouse_Test</c>) on the local
/// SQL Server. The database is dropped and recreated fresh once per test run: the identity schema is
/// migrated + seeded directly, then the host is started so every module applies its own migrations.
/// </summary>
public sealed class WarehouseApiFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString =
        "Server=localhost;Database=MayaProWarehouse_Test;Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    private readonly SemaphoreSlim _resetLock = new(1, 1);
    private bool _isReset;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development so the seeder runs; test connection string so we never touch the real database.
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = TestConnectionString
            });
        });
    }

    /// <summary>
    /// Rebuilds the test database exactly once per run and leaves it fully migrated — every module's
    /// tables exist by the time this returns, whichever test class happens to run first.
    /// </summary>
    public async Task EnsureDatabaseResetAsync()
    {
        if (_isReset)
            return;

        await _resetLock.WaitAsync();
        try
        {
            if (_isReset)
                return;

            await ResetDatabaseAsync();
            await StartHostAsync();
            _isReset = true;
        }
        finally
        {
            _resetLock.Release();
        }
    }

    /// <summary>
    /// Starts the host right after the reset, which is what applies <b>every</b> module's migrations —
    /// <see cref="ResetDatabaseAsync"/> only rebuilds the identity schema, the other modules are migrated
    /// by the host's own startup <c>MigrateModulesAsync()</c>.
    /// <para>
    /// Without this the schema of a module is created lazily, on whatever line first reaches the host. A
    /// test class that touches the database directly before its first HTTP call (as the tenant-isolation
    /// suite does, provisioning shops through <c>TenantTestFixture</c>) would then hit a table that does
    /// not exist yet — passing only when some earlier class happened to boot the host first. Starting the
    /// host here removes that ordering dependency for the whole suite, and keeps the host as the single
    /// source of truth for which migrations exist.
    /// </para>
    /// </summary>
    private async Task StartHostAsync()
    {
        using HttpClient client = CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }

    private static async Task ResetDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            // Match the module's migration-history location, or the host's startup migration
            // would not see these migrations as applied and would try to recreate the tables.
            .UseSqlServer(TestConnectionString, sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema)
                // Dropping + recreating the database and running every module's migrations is a cold-start
                // operation that can exceed the default 30s command timeout on a busy local SQL Server.
                .CommandTimeout(120))
            .Options;

        await using var db = new AuthDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        var seeder = new UserSeeder(db, new BCryptPasswordHasher());
        await seeder.SeedAsync();
    }
}
