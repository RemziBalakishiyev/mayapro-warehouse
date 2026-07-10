namespace MayaPro.WarehouseApi.Modules.Auth.Domain;

/// <summary>
/// System roles. Persisted by name (see UserConfiguration) so the DB stays readable and stable
/// against reordering. The wire contract uses the lowercase frontend codes — see <see cref="RoleCode"/>.
/// </summary>
public enum UserRole
{
    Sahibkar = 1,
    Menecer = 2,
    Satici = 3
}

/// <summary>
/// Maps <see cref="UserRole"/> to the frontend role codes (<c>"sahib" | "menecer" | "satici"</c>),
/// which are the API contract for the <c>role</c> field in DTOs.
/// </summary>
public static class RoleCode
{
    public const string Sahib = "sahib";
    public const string Menecer = "menecer";
    public const string Satici = "satici";

    public static string ToCode(this UserRole role) => role switch
    {
        UserRole.Sahibkar => Sahib,
        UserRole.Menecer => Menecer,
        UserRole.Satici => Satici,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Naməlum rol")
    };
}
