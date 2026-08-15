using System.Data.Common;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.DayEnd.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Infrastructure;

/// <summary>
/// The DayEnd module's DbContext. Owns the <c>dayend</c> schema. Participates in cross-module transactions
/// via <see cref="ITransactionalDbContext"/> so a closing and its activity log commit together.
/// <para>
/// BE#35: closings are tenant-scoped, so <c>OnModelCreating</c> installs the shared global query filter.
/// <paramref name="currentTenant"/> is optional purely so unit tests can new the context up with options
/// alone; in the host it is always injected.
/// </para>
/// </summary>
public sealed class DayEndDbContext(
    DbContextOptions<DayEndDbContext> options,
    ICurrentTenant? currentTenant = null)
    : DbContext(options), IDayEndDbContext, ITransactionalDbContext, ITenantAwareDbContext
{
    public const string Schema = "dayend";

    public Guid CurrentTenantId => currentTenant?.TenantId ?? Guid.Empty;

    public DbSet<Closing> Closings => Set<Closing>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DayEndDbContext).Assembly);
        modelBuilder.ApplyTenantIsolation(this);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
