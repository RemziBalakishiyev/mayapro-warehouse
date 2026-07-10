using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// Boots the real host against a dedicated test database (<c>MayaProWarehouse_Test</c>) on the local
/// SQL Server. The database is dropped, migrated and seeded fresh once per test run, before the host
/// starts, so the host's own startup migration/seed is an idempotent no-op.
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

    /// <summary>Rebuilds the test database exactly once per run, before the host is first used.</summary>
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
            _isReset = true;
        }
        finally
        {
            _resetLock.Release();
        }
    }

    private static async Task ResetDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            // Match the module's migration-history location, or the host's startup migration
            // would not see these migrations as applied and would try to recreate the tables.
            .UseSqlServer(TestConnectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema))
            .Options;

        await using var db = new AuthDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        var seeder = new UserSeeder(db, new BCryptPasswordHasher());
        await seeder.SeedAsync();
    }
}
