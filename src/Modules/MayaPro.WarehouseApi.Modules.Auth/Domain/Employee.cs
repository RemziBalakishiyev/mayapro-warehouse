using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Auth.Domain;

/// <summary>
/// A person on the payroll (BE#57). An employee is a <b>payroll record</b>, not an account: the shop keeps one
/// row per person it pays, and that person never signs in.
/// <para>
/// This is the entity that <see cref="User"/> used to double as, and splitting them is the whole point of
/// BE#57. A <see cref="User"/> answers "who may log in and what may they do"; an <see cref="Employee"/>
/// answers "who do we owe a salary to". The two lists overlap in practice but are not the same list, and
/// conflating them meant a labourer had to be given a password before the owner could record his wage.
/// </para>
/// <para>
/// Consequently there is deliberately <b>no</b> <c>PasswordHash</c>, <c>Role</c> or <c>Email</c> here, and no
/// unique index on <see cref="Phone"/>: the number is a way to call someone, not a login identifier, so two
/// brothers sharing one phone are two perfectly ordinary employees. <see cref="Position"/> is free text
/// ("Satıcı", "Fəhlə", "Sürücü") and must not be confused with the frozen role codes in <c>WireFormat</c>.
/// </para>
/// <para>
/// Employees are never deleted, only deactivated — a salary history that outlived the person's employment is
/// still the shop's accounting record (<see cref="Deactivate"/>).
/// </para>
/// </summary>
public sealed class Employee : TenantEntity
{
    // EF Core constructor.
    private Employee() { }

    private Employee(string fullName, string? phone, string position, decimal monthlySalary, string? note, bool isActive)
    {
        FullName = fullName;
        Phone = phone;
        Position = position;
        MonthlySalary = monthlySalary;
        Note = note;
        IsActive = isActive;
    }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>Contact number only — optional, and explicitly <b>not</b> unique. Stored canonically.</summary>
    public string? Phone { get; private set; }

    /// <summary>Free text job title ("Satıcı", "Fəhlə", "Sürücü"). Not a role code.</summary>
    public string Position { get; private set; } = string.Empty;

    /// <summary>
    /// The agreed monthly salary. Zero means "not agreed yet" — never null, so the summary maths never has to
    /// special-case a missing figure (BE#28 behaviour, carried over unchanged).
    /// </summary>
    public decimal MonthlySalary { get; private set; }

    public string? Note { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static Employee Create(
        string fullName,
        string? phone,
        string position,
        decimal monthlySalary = 0m,
        string? note = null,
        bool isActive = true) =>
        new(fullName, phone, position, monthlySalary, note, isActive);

    /// <summary>Edits the employee's details. The agreed salary moves through <see cref="SetMonthlySalary"/>.</summary>
    public void Update(string fullName, string? phone, string position, decimal monthlySalary, string? note)
    {
        FullName = fullName;
        Phone = phone;
        Position = position;
        MonthlySalary = monthlySalary;
        Note = note;
    }

    /// <summary>Sets the agreed monthly salary. The caller validates the amount (never negative).</summary>
    public void SetMonthlySalary(decimal monthlySalary) => MonthlySalary = monthlySalary;

    public void Activate() => IsActive = true;

    /// <summary>
    /// Marks the employee as no longer working here. Idempotent, and never a delete: the salary lines stay
    /// exactly where they are, and the row keeps appearing in listings with <see cref="IsActive"/> false.
    /// </summary>
    public void Deactivate() => IsActive = false;
}
