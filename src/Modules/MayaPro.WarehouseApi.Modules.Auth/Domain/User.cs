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

    /// <summary>
    /// The employee's agreed monthly salary (BE#28). Zero means "not set yet" — never null, so the
    /// summary maths never has to special-case it. Changed only through <see cref="SetMonthlySalary"/>.
    /// </summary>
    public decimal MonthlySalary { get; private set; }

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

    /// <summary>Sets the agreed monthly salary. The caller validates the amount (never negative).</summary>
    public void SetMonthlySalary(decimal monthlySalary) => MonthlySalary = monthlySalary;
}
