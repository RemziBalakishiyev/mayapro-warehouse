using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Auth.Domain;

/// <summary>
/// A login account. Behaviour-rich entity — no public setters; state changes go through methods.
/// Password is only ever stored as a BCrypt hash.
/// <para>
/// BE#57 — a user is <b>not</b> a payroll record. The agreed monthly salary moved to <see cref="Employee"/>,
/// which is what <c>/api/employees</c> and the salary summary now read, because most people a shop pays never
/// sign in at all. What stays here is only what a login needs: the phone identifier, the hash and the role.
/// </para>
/// </summary>
public sealed class User : TenantEntity
{
    // EF Core constructor.
    private User() { }

    private User(string fullName, string phone, string? email, string passwordHash, UserRole role, bool isActive)
    {
        FullName = fullName;
        Phone = phone;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = isActive;
    }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>Login identifier; unique across all users.</summary>
    public string Phone { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static User Create(
        string fullName,
        string phone,
        string? email,
        string passwordHash,
        UserRole role,
        bool isActive = true) =>
        new(fullName, phone, email, passwordHash, role, isActive);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
