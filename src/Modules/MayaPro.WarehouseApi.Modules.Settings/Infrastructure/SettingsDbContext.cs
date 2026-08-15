using MayaPro.WarehouseApi.Modules.Settings.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Settings.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Settings.Infrastructure;

/// <summary>
/// The Settings module's DbContext. Owns the <c>settings</c> schema. Standalone — settings changes are
/// not part of any cross-module chain, so it does not enlist in the shared transaction.
/// <para>
/// BE#35: the settings row is tenant-scoped (one per shop), so <c>OnModelCreating</c> installs the shared
/// global query filter — which is precisely what makes the handlers' "just take the first row" reads
/// unambiguous again. <paramref name="currentTenant"/> is optional purely so unit tests can new the context
/// up with options alone; in the host it is always injected.
/// </para>
/// </summary>
public sealed class SettingsDbContext(
    DbContextOptions<SettingsDbContext> options,
    ICurrentTenant? currentTenant = null)
    : DbContext(options), ISettingsDbContext, ITenantAwareDbContext
{
    public const string Schema = "settings";

    public Guid CurrentTenantId => currentTenant?.TenantId ?? Guid.Empty;

    public DbSet<StoreSettings> StoreSettings => Set<StoreSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SettingsDbContext).Assembly);
        modelBuilder.ApplyTenantIsolation(this);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
