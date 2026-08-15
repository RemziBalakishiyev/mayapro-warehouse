namespace MayaPro.WarehouseApi.SharedKernel.Domain;

/// <summary>
/// Base type for every business entity that belongs to a tenant (BE#35). Adds the <see cref="TenantId"/>
/// column on top of <see cref="Entity"/>'s identity and audit timestamps.
/// <para>
/// <see cref="TenantId"/> has a private setter on purpose: use cases must never assign it. It is written
/// once, by <c>TenantInterceptor</c>, when the row is inserted, and the same interceptor rejects any later
/// change. The only other legitimate writer is infrastructure that runs without an HTTP request (seeders,
/// data migrations, tests), which calls <see cref="AssignTenant"/> explicitly.
/// </para>
/// </summary>
public abstract class TenantEntity : Entity, ITenantScoped
{
    public Guid TenantId { get; private set; }

    /// <summary>
    /// Infrastructure-only: assigns the owning tenant. Called by <c>TenantInterceptor</c> on insert and by
    /// context-free code paths (startup seeders, tests) that have to name the tenant themselves. Business
    /// code must not call this — the interceptor already does it for every request-scoped write.
    /// </summary>
    public void AssignTenant(Guid tenantId) => TenantId = tenantId;
}
