using System.Data.Common;
using MayaPro.WarehouseApi.Modules.Activity.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Activity.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Activity.Infrastructure;

/// <summary>
/// The Activity module's DbContext. Owns the <c>activity</c> schema. Participates in cross-module
/// transactions via <see cref="ITransactionalDbContext"/> — every chain that logs an activity writes it
/// into the same shared transaction, so the log commits atomically with the operation.
/// <para>
/// BE#35: activity rows are tenant-scoped, so <c>OnModelCreating</c> installs the shared global query
/// filter. <paramref name="currentTenant"/> is optional purely so unit tests can new the context up with
/// options alone; in the host it is always injected.
/// </para>
/// </summary>
public sealed class ActivityDbContext(
    DbContextOptions<ActivityDbContext> options,
    ICurrentTenant? currentTenant = null)
    : DbContext(options), IActivityDbContext, ITransactionalDbContext, ITenantAwareDbContext
{
    public const string Schema = "activity";

    public Guid CurrentTenantId => currentTenant?.TenantId ?? Guid.Empty;

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ActivityDbContext).Assembly);
        modelBuilder.ApplyTenantIsolation(this);
    }
}
