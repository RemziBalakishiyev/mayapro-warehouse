using System.Security.Claims;
using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Api.Security;

/// <summary>
/// Resolves the current request's tenant, in this order:
/// <list type="number">
///   <item>an explicit <see cref="TenantScope"/> override — used by the one anonymous surface that resolves
///   its tenant from data instead of a token (the public invoice link);</item>
///   <item>the JWT <c>tenantId</c> claim.</item>
/// </list>
/// Inbound claim mapping is disabled on the bearer handler, so the claim arrives under its raw name — the
/// same string the Auth module's <c>TokenService</c> writes. Anonymous requests simply have no tenant; this
/// never throws (the host's tenant gate is what turns "authenticated but tenant-less" into a 401).
/// </summary>
public sealed class CurrentTenant(IHttpContextAccessor httpContextAccessor, TenantScope tenantScope) : ICurrentTenant
{
    /// <summary>The JWT claim carrying the tenant id. Mirrors <c>TokenService.TenantClaim</c>.</summary>
    public const string TenantClaimType = "tenantId";

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? TenantId
    {
        get
        {
            if (tenantScope.TenantId is { } scoped)
                return scoped;

            string? claim = Principal?.FindFirstValue(TenantClaimType);
            return Guid.TryParse(claim, out Guid id) && id != Guid.Empty ? id : null;
        }
    }

    public bool HasTenant => TenantId is not null;
}
