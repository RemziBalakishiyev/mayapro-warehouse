namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>
/// The salary side of the Auth (employees) module's public surface for other modules — deliberately shaped
/// exactly like <see cref="IExpensesModule"/>: a day total for day-end closing and rows over a range for the
/// reports dashboard.
/// <para>
/// BE#28: only <c>payment</c> entries are ever returned. A payment is real money leaving the drawer, so it
/// belongs in the day's cash arithmetic; a <c>deduction</c> is charged against the employee's account only
/// and must never reach the cash figures.
/// </para>
/// </summary>
public interface ISalaryModule
{
    /// <summary>Sums a business day's salary payments. Used by day-end closing.</summary>
    Task<decimal> GetDayPaymentsTotalAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the salary payments in the inclusive date range (both bounds optional). Used by the read-only
    /// Reports module, whose "expected cash" works over a range (since the last closing) rather than one day.
    /// </summary>
    Task<IReadOnlyList<SalaryPaymentRow>> GetPaymentsAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single salary payment as seen by reports: the business-zone date the money left the drawer, the
/// employee it was paid to and the amount. Deductions are never represented here.
/// </summary>
public sealed record SalaryPaymentRow(DateOnly Date, Guid UserId, string FullName, decimal Amount);
