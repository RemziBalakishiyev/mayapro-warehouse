using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure;

/// <summary>
/// Issues HS256-signed JWTs. Role is emitted as the enum name (Owner/Manager/Seller) so server-side
/// role policies match directly; the frontend-facing role code lives only in DTOs.
/// <para>
/// BE#35: the token also carries <see cref="TenantClaim"/> — the shop the user belongs to. It is the only
/// source of tenant identity for an authenticated request, so every protected endpoint is rejected when it
/// is missing (the host's tenant gate), and no request can ever address another shop's data.
/// </para>
/// </summary>
public sealed class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    /// <summary>
    /// The tenant claim's name. Raw (not a legacy URI) because the bearer handler disables inbound claim
    /// mapping — <c>CurrentTenant</c> reads exactly this string.
    /// </summary>
    public const string TenantClaim = "tenantId";

    private readonly JwtOptions _options = options.Value;

    public string CreateToken(User user, bool rememberMe = false)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("name", user.FullName),
            new Claim("role", user.Role.ToString()),
            new Claim(TenantClaim, user.TenantId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        // BE#45: "remember me" swaps the short-lived default for Jwt:RememberMeExpiryDays.
        DateTime expires = rememberMe
            ? DateTime.UtcNow.AddDays(_options.RememberMeExpiryDays)
            : DateTime.UtcNow.AddHours(_options.ExpiryHours);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
