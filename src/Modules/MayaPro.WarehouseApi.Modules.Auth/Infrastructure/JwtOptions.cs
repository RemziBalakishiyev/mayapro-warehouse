namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure;

/// <summary>Binds the <c>Jwt</c> configuration section. Shared by token issuance and bearer validation.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>Symmetric signing key; must be at least 32 characters (256 bits) for HS256.</summary>
    public string Secret { get; init; } = string.Empty;

    public int ExpiryHours { get; init; } = 24;
}
