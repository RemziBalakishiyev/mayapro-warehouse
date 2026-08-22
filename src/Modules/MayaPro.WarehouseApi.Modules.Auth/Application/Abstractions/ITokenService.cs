using MayaPro.WarehouseApi.Modules.Auth.Domain;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;

/// <summary>Issues signed JWT access tokens for authenticated users.</summary>
public interface ITokenService
{
    /// <summary>
    /// Creates a signed JWT with <c>sub</c>, <c>name</c> and <c>role</c> claims. When
    /// <paramref name="rememberMe"/> is <c>true</c> the token expires after <c>Jwt:RememberMeExpiryDays</c>
    /// instead of the usual <c>Jwt:ExpiryHours</c> (BE#45).
    /// </summary>
    string CreateToken(User user, bool rememberMe = false);
}
