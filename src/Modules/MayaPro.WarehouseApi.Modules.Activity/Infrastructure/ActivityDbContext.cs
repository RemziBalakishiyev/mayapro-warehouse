using System.Data.Common;
using MayaPro.WarehouseApi.Modules.Activity.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Activity.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Activity.Infrastructure;

/// <summary>
/// The Activity module's DbContext. Owns the <c>activity</c> schema. Participates in cross-module
/// transactions via <see cref="ITransactionalDbContext"/> — every chain that logs an activity writes it
/// into the same shared transaction, so the log commits atomically with the operation.
/// </summary>
public sealed class ActivityDbContext(DbContextOptions<ActivityDbContext> options)
    : DbContext(options), IActivityDbContext, ITransactionalDbContext
{
    public const string Schema = "activity";

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ActivityDbContext).Assembly);
    }
}
