using System.Data.Common;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Infrastructure;

/// <summary>
/// The Suppliers module's DbContext. Owns the <c>suppliers</c> schema. Participates in cross-module
/// transactions via <see cref="ITransactionalDbContext"/>.
/// <para>
/// BE#35: every entity here is tenant-scoped, so <c>OnModelCreating</c> installs the shared global query
/// filter. <paramref name="currentTenant"/> is optional purely so unit tests can new the context up with
/// options alone; in the host it is always injected.
/// </para>
/// </summary>
public sealed class SuppliersDbContext(
    DbContextOptions<SuppliersDbContext> options,
    ICurrentTenant? currentTenant = null)
    : DbContext(options), ISuppliersDbContext, ITransactionalDbContext, ITenantAwareDbContext
{
    public const string Schema = "suppliers";

    public Guid CurrentTenantId => currentTenant?.TenantId ?? Guid.Empty;

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();

    public DbSet<SupplierDebtAdjustment> SupplierDebtAdjustments => Set<SupplierDebtAdjustment>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SuppliersDbContext).Assembly);
        modelBuilder.ApplyTenantIsolation(this);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
