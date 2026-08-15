namespace MayaPro.WarehouseApi.SharedKernel.Application;

/// <summary>
/// The tenant (mağaza) the current request belongs to, resolved from the JWT's <c>tenantId</c> claim.
/// Implemented in the API host over <c>HttpContext</c>, exactly like <see cref="ICurrentUser"/>, so modules
/// and the SharedKernel never reference ASP.NET Core HTTP types.
/// <para>
/// Anonymous requests simply have no tenant (<see cref="TenantId"/> is <c>null</c>) — this never throws.
/// Query filters then match nothing and the interceptor refuses to insert, which is the safe direction.
/// </para>
/// </summary>
public interface ICurrentTenant
{
    /// <summary>The tenant id (JWT <c>tenantId</c> claim), or <c>null</c> when there is no tenant context.</summary>
    Guid? TenantId { get; }

    /// <summary>True when a tenant could be resolved for the current execution.</summary>
    bool HasTenant { get; }
}
