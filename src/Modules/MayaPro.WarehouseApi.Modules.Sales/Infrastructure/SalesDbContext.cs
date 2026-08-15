using System.Data.Common;
using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Sales.Infrastructure;

/// <summary>
/// The Sales module's DbContext. Owns the <c>sales</c> schema. Participates in cross-module transactions
/// via <see cref="ITransactionalDbContext"/> — this is what lets a sale share the stock/debt transaction.
/// <para>
/// BE#35: sales are tenant-scoped, so <c>OnModelCreating</c> installs the shared global query filter.
/// <paramref name="currentTenant"/> is optional purely so unit tests can new the context up with options
/// alone; in the host it is always injected.
/// </para>
/// </summary>
public sealed class SalesDbContext(
    DbContextOptions<SalesDbContext> options,
    ICurrentTenant? currentTenant = null)
    : DbContext(options), ISalesDbContext, ITransactionalDbContext, ITenantAwareDbContext
{
    public const string Schema = "sales";

    public Guid CurrentTenantId => currentTenant?.TenantId ?? Guid.Empty;

    public DbSet<Sale> Sales => Set<Sale>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);
        modelBuilder.ApplyTenantIsolation(this);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
