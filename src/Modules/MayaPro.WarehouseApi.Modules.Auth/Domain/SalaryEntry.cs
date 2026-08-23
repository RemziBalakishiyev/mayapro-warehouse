using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Auth.Domain;

/// <summary>
/// One line on an employee's salary account (BE#28): either a payment handed over (salary, advance) or a
/// deduction charged against it (meals, transport, a fine).
/// <para>
/// <see cref="Date"/> and <see cref="Month"/> answer two different questions and never substitute for one
/// another: <see cref="Date"/> is the UTC instant the cash actually moved (day-end and the dashboard filter
/// on it through the business-zone day window), while <see cref="Month"/> is the <c>yyyy-MM</c> accounting
/// month the line settles (the salary summary filters on it). Paying July's salary on 1 August is one row
/// with an August date and a July month.
/// </para>
/// </summary>
public sealed class SalaryEntry : TenantEntity
{
    // EF Core constructor.
    private SalaryEntry() { }

    private SalaryEntry(
        Guid employeeId,
        SalaryEntryType type,
        decimal amount,
        string? note,
        DateTime date,
        string month,
        Guid? createdByUserId)
    {
        EmployeeId = employeeId;
        Type = type;
        Amount = amount;
        Note = note;
        Date = date;
        Month = month;
        CreatedByUserId = createdByUserId;
    }

    /// <summary>
    /// The payroll record this line belongs to — an <see cref="Employee"/> since BE#57, never a login account.
    /// "Who was paid" and "who paid" are two different questions: this is the first,
    /// <see cref="CreatedByUserId"/> is the second, and they must never be merged.
    /// </summary>
    public Guid EmployeeId { get; private set; }

    public SalaryEntryType Type { get; private set; }

    public decimal Amount { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The UTC instant the money moved — the cash-side timestamp.</summary>
    public DateTime Date { get; private set; }

    /// <summary>The accounting month (<c>yyyy-MM</c>) this line is booked against.</summary>
    public string Month { get; private set; } = string.Empty;

    /// <summary>The login account that recorded the line (<c>ICurrentUser.UserId</c>) — who handed the money over.</summary>
    public Guid? CreatedByUserId { get; private set; }

    public static SalaryEntry Create(
        Guid employeeId,
        SalaryEntryType type,
        decimal amount,
        string? note,
        DateTime date,
        string month,
        Guid? createdByUserId) =>
        new(employeeId, type, amount, note, date, month, createdByUserId);
}
