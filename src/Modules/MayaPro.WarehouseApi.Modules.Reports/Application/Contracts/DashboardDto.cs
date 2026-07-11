namespace MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;

/// <summary>
/// The dashboard snapshot: current stock health, today's trading, and outstanding receivables. Every
/// figure is computed server-side from the other modules' contracts — the frontend only displays it.
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
    decimal TotalCustomerDebt);
