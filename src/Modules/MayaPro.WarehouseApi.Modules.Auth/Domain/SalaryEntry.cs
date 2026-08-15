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
        Guid userId,
        SalaryEntryType type,
        decimal amount,
        string? note,
        DateTime date,
        string month,
        Guid? createdByUserId)
    {
        UserId = userId;
        Type = type;
        Amount = amount;
        Note = note;
        Date = date;
        Month = month;
        CreatedByUserId = createdByUserId;
    }

    /// <summary>The employee this line belongs to.</summary>
    public Guid UserId { get; private set; }

    public SalaryEntryType Type { get; private set; }

    public decimal Amount { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The UTC instant the money moved — the cash-side timestamp.</summary>
    public DateTime Date { get; private set; }

    /// <summary>The accounting month (<c>yyyy-MM</c>) this line is booked against.</summary>
    public string Month { get; private set; } = string.Empty;

    public Guid? CreatedByUserId { get; private set; }

    public static SalaryEntry Create(
        Guid userId,
        SalaryEntryType type,
        decimal amount,
        string? note,
        DateTime date,
        string month,
        Guid? createdByUserId) =>
        new(userId, type, amount, note, date, month, createdByUserId);
}
