using MayaPro.WarehouseApi.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MayaPro.WarehouseApi.SharedKernel.Infrastructure;

/// <summary>
/// Sets <see cref="Entity.CreatedAt"/> on insert and <see cref="Entity.UpdatedAt"/> on insert/update,
/// so modules never have to touch audit timestamps by hand. Registered on every module DbContext.
/// </summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ApplyAuditInfo(DbContext? context)
    {
        if (context is null)
            return;

        var now = DateTime.UtcNow;

        foreach (EntityEntry<Entity> entry in context.ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
