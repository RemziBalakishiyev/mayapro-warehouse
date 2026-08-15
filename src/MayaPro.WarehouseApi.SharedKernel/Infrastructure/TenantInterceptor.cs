using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MayaPro.WarehouseApi.SharedKernel.Infrastructure;

/// <summary>
/// Writes <see cref="ITenantScoped.TenantId"/> on insert and protects it afterwards — the write-side half
/// of the isolation mechanism (the read-side half is the global query filter). Same shape and registration
/// as <see cref="AuditInterceptor"/>, so no use case ever mentions the tenant.
/// <para>Rules, in the order they are applied:</para>
/// <list type="number">
///   <item><b>Added</b> with an unset tenant → stamped with the current tenant. An entity that already
///   carries a tenant (a seeder or data-fix that assigned it explicitly) is left alone.</item>
///   <item><b>Added</b> with an unset tenant and <b>no</b> tenant context → <see cref="MissingTenantContextException"/>.
///   Writing <c>Guid.Empty</c> instead would create rows no tenant can ever see.</item>
///   <item><b>Modified</b> with a changed tenant → the change is reverted to the stored value (silently, so
///   an unrelated update in the same SaveChanges still succeeds). Moving a row between tenants is not a
///   supported operation.</item>
/// </list>
/// </summary>
public sealed class TenantInterceptor(ICurrentTenant currentTenant) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyTenant(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyTenant(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyTenant(DbContext? context)
    {
        if (context is null)
            return;

        foreach (EntityEntry<TenantEntity> entry in context.ChangeTracker.Entries<TenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    StampNewRow(entry);
                    break;
                case EntityState.Modified:
                    RevertTenantChange(entry);
                    break;
            }
        }
    }

    private void StampNewRow(EntityEntry<TenantEntity> entry)
    {
        if (entry.Entity.TenantId != Guid.Empty)
            return;

        if (currentTenant.TenantId is not { } tenantId || tenantId == Guid.Empty)
            throw new MissingTenantContextException(entry.Entity.GetType().Name);

        entry.Entity.AssignTenant(tenantId);
    }

    private static void RevertTenantChange(EntityEntry<TenantEntity> entry)
    {
        PropertyEntry<TenantEntity, Guid> tenantProperty = entry.Property(e => e.TenantId);
        if (!tenantProperty.IsModified)
            return;

        tenantProperty.CurrentValue = tenantProperty.OriginalValue;
        tenantProperty.IsModified = false;
    }
}
