namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>The Expenses module's public surface for other modules — day total for day-end and rows for reports.</summary>
public interface IExpensesModule
{
    /// <summary>Sums a day's expenses. Used by day-end closing.</summary>
    Task<decimal> GetDayTotalAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the expenses in the inclusive date range (both bounds optional). Used by the read-only
    /// Reports module to total expenses over a period without touching the expenses table.
    /// </summary>
    Task<IReadOnlyList<ExpenseReportRow>> GetExpensesAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);
}

/// <summary>A single expense as seen by reports: date, category code and amount.</summary>
public sealed record ExpenseReportRow(DateOnly Date, string Category, decimal Amount);
