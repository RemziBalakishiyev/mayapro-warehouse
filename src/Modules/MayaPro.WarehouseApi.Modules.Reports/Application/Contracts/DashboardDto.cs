namespace MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;

/// <summary>
/// The dashboard snapshot: stock health, today's trading, receivables and payables, expected cash in the
/// drawer, frozen (slow-moving) stock, best sellers and low-stock items. Every figure is computed
/// server-side from the other modules' contracts — the frontend only displays it.
/// </summary>
public sealed record DashboardDto(
    int ProductCount,
    int LowStockCount,
    int OutOfStockCount,
    decimal StockCostValue,
    decimal StockRetailValue,
    decimal TodaySales,
    decimal TodayProfit,
    decimal TodayExpenses,
    int TodaySalesCount,
    decimal TotalCustomerDebt,
    decimal TotalSupplierDebt,
    decimal ExpectedCash,
    FrozenProductsDto FrozenProducts,
    IReadOnlyList<TopProductDto> TopProducts,
    IReadOnlyList<LowStockProductDto> LowStock);

/// <summary>
/// Counts of in-stock products that have not sold recently, by how long they have been frozen. The
/// buckets are cumulative: a product frozen 95 days counts in all three.
/// </summary>
public sealed record FrozenProductsDto(int Days30, int Days60, int Days90);

/// <summary>A best-selling product: total units sold and revenue over all time.</summary>
public sealed record TopProductDto(Guid ProductId, string Name, int QuantitySold, decimal Revenue);

/// <summary>A product at or below its reorder threshold.</summary>
public sealed record LowStockProductDto(Guid ProductId, string Name, int Quantity, int MinStock);
