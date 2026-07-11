namespace MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;

/// <summary>
/// The dashboard snapshot: stock health, today's trading, receivables/payables, expected drawer cash,
/// frozen (slow-moving) stock, best sellers, low-stock items, the daily/monthly trend series and the
/// recent activity feed. Everything is computed server-side so the frontend never pulls raw collections.
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
    IReadOnlyList<LowStockProductDto> LowStock,
    IReadOnlyList<DailyPointDto> DailySeries,
    IReadOnlyList<MonthlyPointDto> MonthlySeries,
    IReadOnlyList<RecentSaleDto> RecentSales,
    IReadOnlyList<RecentPaymentDto> RecentPayments);

/// <summary>
/// Frozen (slow-moving) stock: cumulative counts by staleness (a product frozen 95 days counts in all
/// three) plus the per-product detail so the frontend need not compute it.
/// </summary>
public sealed record FrozenProductsDto(
    int Days30,
    int Days60,
    int Days90,
    IReadOnlyList<FrozenProductDto> Items);

/// <summary>A frozen product: its on-hand stock, the capital tied up in it, and idle days (null = never sold).</summary>
public sealed record FrozenProductDto(Guid Id, string Name, int Quantity, decimal FrozenValue, int? DaysSinceLastSale);

/// <summary>A best-selling product: total units sold and revenue over all time.</summary>
public sealed record TopProductDto(Guid ProductId, string Name, int QuantitySold, decimal Revenue);

/// <summary>A product at or below its reorder threshold.</summary>
public sealed record LowStockProductDto(Guid ProductId, string Name, int Quantity, int MinStock);

/// <summary>One day of the 14-day trend: net sales and profit.</summary>
public sealed record DailyPointDto(DateOnly Date, decimal Sales, decimal Profit);

/// <summary>One month of the 6-month trend: profit. Month is <c>yyyy-MM</c>.</summary>
public sealed record MonthlyPointDto(string Month, decimal Profit);

/// <summary>A recent sale for the activity feed.</summary>
public sealed record RecentSaleDto(
    Guid Id,
    DateOnly Date,
    string ProductName,
    int Quantity,
    decimal TotalAmount,
    string PaymentType);

/// <summary>A recent customer payment for the activity feed.</summary>
public sealed record RecentPaymentDto(Guid Id, DateOnly Date, string CustomerName, decimal Amount);
