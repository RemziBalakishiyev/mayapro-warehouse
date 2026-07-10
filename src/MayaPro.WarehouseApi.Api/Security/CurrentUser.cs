using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Api.Security;

/// <summary>
/// Reads the authenticated caller from the current request's JWT claims. Inbound claim mapping is
/// disabled on the bearer handler, so <c>sub</c> and <c>role</c> arrive under their raw names.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            string? sub = Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(sub, out Guid id) ? id : null;
        }
    }

    public string? Role => Principal?.FindFirstValue("role");

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
}
