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
    Seller = 3
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

    public static string ToCode(this UserRole role) => role switch
    {
        UserRole.Owner => Owner,
        UserRole.Manager => Manager,
        UserRole.Seller => Seller,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Naməlum rol")
    };
}
