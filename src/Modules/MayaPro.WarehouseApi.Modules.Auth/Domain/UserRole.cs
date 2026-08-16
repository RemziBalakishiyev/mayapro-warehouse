using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Auth.Domain;

/// <summary>
/// System roles. Persisted by name (see UserConfiguration) so the DB stays readable and stable
/// against reordering. The wire contract uses the lowercase frontend codes — see <see cref="RoleCode"/>.
/// </summary>
public enum UserRole
{
    Owner = 1,
    Manager = 2,
    Seller = 3,

    /// <summary>
    /// BE#36 — the platform operator. Not a shop role: this user belongs to no <c>tenancy.Tenants</c> row
    /// (it carries <c>TenantDefaults.PlatformTenantId</c>, an id no shop uses) and its only surface is
    /// <c>/api/admin/*</c>. Persisted by name like the others, so the numeric value is incidental.
    /// </summary>
    PlatformAdmin = 4
}

/// <summary>
/// Maps <see cref="UserRole"/> to the frontend role codes (<c>"sahib" | "menecer" | "satici"</c>),
/// which are the API contract for the <c>role</c> field in DTOs. The code values live in
/// <see cref="WireFormat"/> (single source of truth).
/// </summary>
public static class RoleCode
{
    public const string Owner = WireFormat.Roles.Owner;
    public const string Manager = WireFormat.Roles.Manager;
    public const string Seller = WireFormat.Roles.Seller;
    public const string PlatformAdmin = WireFormat.Roles.PlatformAdmin;

    public static string ToCode(this UserRole role) => role switch
    {
        UserRole.Owner => Owner,
        UserRole.Manager => Manager,
        UserRole.Seller => Seller,
        UserRole.PlatformAdmin => PlatformAdmin,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Naməlum rol")
    };
}
