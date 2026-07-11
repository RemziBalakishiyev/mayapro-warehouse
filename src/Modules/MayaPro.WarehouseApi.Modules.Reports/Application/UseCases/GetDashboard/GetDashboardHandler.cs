using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDashboard;

/// <summary>
/// Builds the dashboard from the other modules' read contracts: stock value and low-stock counts from
/// Products, today's revenue/profit from Sales, today's spend from Expenses, receivables from Customers.
/// The Reports module owns no tables — this is the whole point of the read-only design.
/// </summary>
public sealed class GetDashboardHandler(
    IProductsModule products,
    ISalesModule sales,
    IExpensesModule expenses,
    ICustomersModule customers)
{
    public async Task<DashboardDto> Handle(DateOnly today, CancellationToken ct)
    {
        IReadOnlyList<ProductSnapshot> snapshots = await products.GetAllSnapshotsAsync(ct);
        IReadOnlyList<SalesReportRow> todaySales = await sales.GetSalesAsync(today, today, ct);
        decimal todayExpenses = await expenses.GetDayTotalAsync(today, ct);
        decimal totalDebt = await customers.GetTotalDebtAsync(ct);

        return new DashboardDto(
            ProductCount: snapshots.Count,
            LowStockCount: snapshots.Count(p => p.Quantity > 0 && p.Quantity <= p.MinStock),
            OutOfStockCount: snapshots.Count(p => p.Quantity <= 0),
            StockCostValue: snapshots.Sum(p => p.Quantity * p.RealCostPerUnit),
            StockRetailValue: snapshots.Sum(p => p.Quantity * p.SalePrice),
            TodaySales: todaySales.Sum(s => s.TotalAmount),
            TodayProfit: todaySales.Sum(s => s.Profit),
            TodayExpenses: todayExpenses,
            TodaySalesCount: todaySales.Count,
            TotalCustomerDebt: totalDebt);
    }
}
