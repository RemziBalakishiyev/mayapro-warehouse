namespace MayaPro.WarehouseApi.SharedKernel.Infrastructure;

/// <summary>
/// Implemented by every module DbContext that owns tenant-scoped tables. The single member is what the
/// generated global query filters compare against.
/// <para>
/// It has to be a member <b>of the DbContext itself</b>: EF Core rewrites references to the context
/// instance inside a query filter into a per-execution query parameter, which is what lets one cached model
/// serve every request while each request still filters by its own tenant. Reading the value from a
/// captured service object instead would bake the first request's service into the cached model.
/// </para>
/// </summary>
public interface ITenantAwareDbContext
{
    /// <summary>
    /// The tenant every query is scoped to — <c>Guid.Empty</c> when there is no tenant context, which
    /// deliberately matches no row.
    /// </summary>
    Guid CurrentTenantId { get; }
}
