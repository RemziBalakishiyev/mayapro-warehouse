using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Auth.Domain;

/// <summary>
/// A system user (employee). Behaviour-rich entity — no public setters; state changes go through methods.
/// Password is only ever stored as a BCrypt hash.
/// </summary>
public sealed class User : Entity
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
