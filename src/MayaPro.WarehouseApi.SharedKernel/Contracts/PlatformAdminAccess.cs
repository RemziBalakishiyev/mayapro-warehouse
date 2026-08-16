namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>
/// BE#36 — the two strings that describe the platform operator's access, in one place so the host (which
/// declares the policy and lets the token through the tenant gate) and the Tenancy module (which guards
/// <c>/api/admin/*</c> with it) cannot drift apart. A module may not reference the host, and the host may
/// not reference a module's enum, so SharedKernel is the only home they share.
/// </summary>
public static class PlatformAdminAccess
{
    /// <summary>
    /// The value of the JWT <c>role</c> claim for a platform admin — the <c>UserRole</c> enum name, exactly
    /// as <c>TokenService</c> writes it and as the bearer handler's role claim type reads it.
    /// </summary>
    public const string RoleName = "PlatformAdmin";

    /// <summary>The authorization policy every <c>/api/admin/*</c> endpoint requires.</summary>
    public const string Policy = "PlatformAdminOnly";
}
