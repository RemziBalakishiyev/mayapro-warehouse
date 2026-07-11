using MayaPro.WarehouseApi.Modules.Settings.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Settings.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Settings.Infrastructure;

/// <summary>
/// The Settings module's DbContext. Owns the <c>settings</c> schema. Standalone — settings changes are
/// not part of any cross-module chain, so it does not enlist in the shared transaction.
/// </summary>
public sealed class SettingsDbContext(DbContextOptions<SettingsDbContext> options)
    : DbContext(options), ISettingsDbContext
{
    public const string Schema = "settings";

    public DbSet<StoreSettings> StoreSettings => Set<StoreSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SettingsDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
