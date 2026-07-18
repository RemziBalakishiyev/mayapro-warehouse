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

    /// <summary>Returns the <paramref name="take"/> most recent sales for the dashboard's activity feed.</summary>
    Task<IReadOnlyList<RecentSaleInfo>> GetRecentSalesAsync(int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the last credit (Nisyə) sale date per customer. Used by the Customers module to show each
    /// customer's last purchase date without joining across module tables.
    /// </summary>
    Task<IReadOnlyList<CustomerLastPurchase>> GetLastCreditSaleDatesByCustomerAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>A day's net sales split by payment type.</summary>
public sealed record SalesDayTotals(decimal Cash, decimal Card, decimal Credit);

/// <summary>The most recent sale date for a product.</summary>
public sealed record ProductLastSale(Guid ProductId, DateOnly LastSale);

/// <summary>
/// A recent sale for the dashboard feed. Date is the business-zone (local) date. <see cref="CustomerId"/>
/// is set only for credit (Nisyə) sales; null for cash and card sales. <see cref="Category"/> is the
/// sale-time snapshot (null on older rows or when a manual sale omitted it).
/// </summary>
public sealed record RecentSaleInfo(
    Guid Id,
    DateOnly Date,
    string ProductName,
    string? Category,
    int Quantity,
    decimal TotalAmount,
    string PaymentType,
    Guid? CustomerId);

/// <summary>A customer's most recent credit-purchase timestamp (UTC).</summary>
public sealed record CustomerLastPurchase(Guid CustomerId, DateTime Date);

/// <summary>
/// A single sale as seen by reports: date, net amount, profit, payment code and product line.
/// <see cref="Profit"/> is null when the sale's cost is unknown (a free-form sale with no cost) — reports
/// exclude it from profit sums instead of counting it as zero. <see cref="ProductId"/> is null for a
/// free-form sale, which keeps such sales out of per-product aggregates (top products, frozen stock).
/// </summary>
public sealed record SalesReportRow(
    DateOnly Date,
    decimal TotalAmount,
    decimal? Profit,
    string PaymentType,
    Guid? ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Discount,
    bool IsManual);
