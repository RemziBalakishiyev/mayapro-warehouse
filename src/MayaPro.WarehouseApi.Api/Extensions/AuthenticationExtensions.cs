using System.Text;
using MayaPro.WarehouseApi.Api.Security;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace MayaPro.WarehouseApi.Api.Extensions;

/// <summary>
/// Wires JWT bearer authentication, role policies and the <see cref="ICurrentUser"/> accessor.
/// The signing key / issuer / audience come from the shared <c>Jwt</c> configuration section, so the
/// host validates exactly what the Auth module's TokenService issues.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>Sahibkar only — day-end, settings, employees management.</summary>
    public const string OwnerOnly = "OwnerOnly";

    /// <summary>Sahibkar or Menecer — products, expenses.</summary>
    public const string OwnerOrManager = "OwnerOrManager";

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string secret = configuration["Jwt:Secret"]
                        ?? throw new InvalidOperationException("Jwt:Secret konfiqurasiyada yoxdur");
        string issuer = configuration["Jwt:Issuer"] ?? string.Empty;
        string audience = configuration["Jwt:Audience"] ?? string.Empty;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep raw claim names (sub, role) instead of the legacy long URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(OwnerOnly, policy => policy.RequireRole(nameof(Roles.Sahibkar)))
            .AddPolicy(OwnerOrManager, policy =>
                policy.RequireRole(nameof(Roles.Sahibkar), nameof(Roles.Menecer)));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }

    // Local mirror of the role names used in JWT role claims — avoids a host → module type dependency.
    private enum Roles
    {
        Sahibkar,
        Menecer,
        Satici
    }
}
