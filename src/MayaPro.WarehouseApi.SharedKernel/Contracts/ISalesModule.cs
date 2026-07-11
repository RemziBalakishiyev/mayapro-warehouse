namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>The Sales module's public surface for other modules — day totals for day-end and rows for reports.</summary>
public interface ISalesModule
{
    /// <summary>Sums a day's sales by payment type (net amounts). Used by day-end closing.</summary>
    Task<SalesDayTotals> GetDayTotalsAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the sales in the inclusive date range (both bounds optional). Used by the read-only Reports
    /// module to compute revenue and profit over a period without touching the sales table.
    /// </summary>
    Task<IReadOnlyList<SalesReportRow>> GetSalesAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the last sale date per product (only products that have ever sold). Used by the read-only
    /// Reports module to flag "frozen" stock — items that have not moved in 30/60/90 days.
    /// </summary>
    Task<IReadOnlyList<ProductLastSale>> GetLastSaleDatesAsync(CancellationToken cancellationToken = default);
}

/// <summary>A day's net sales split by payment type.</summary>
public sealed record SalesDayTotals(decimal Cash, decimal Card, decimal Nisye);

/// <summary>The most recent sale date for a product.</summary>
public sealed record ProductLastSale(Guid ProductId, DateOnly LastSale);

/// <summary>A single sale as seen by reports: date, net amount, profit, payment code and product line.</summary>
public sealed record SalesReportRow(
    DateOnly Date,
    decimal TotalAmount,
    decimal Profit,
    string PaymentType,
    Guid ProductId,
    string ProductName,
    int Quantity);
