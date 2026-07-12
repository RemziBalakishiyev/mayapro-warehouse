using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDashboard;

/// <summary>
/// Fetches everything the dashboard needs from the other modules' read contracts (Products, Sales,
/// Expenses, Customers, Suppliers, DayEnd) and hands it to the pure <see cref="DashboardCalculator"/>.
/// "Today" is the business-zone day. The Reports module owns no tables — this is the read-only design.
/// </summary>
public sealed class GetDashboardHandler(
    IProductsModule products,
    ISalesModule sales,
    IExpensesModule expenses,
    ICustomersModule customers,
    ISuppliersModule suppliers,
    IDayEndModule dayEnd,
    IDateProvider dateProvider)
{
    private const int RecentCount = 5;

    public async Task<DashboardDto> Handle(CancellationToken ct)
    {
        DateOnly today = dateProvider.Today;

        IReadOnlyList<ProductSnapshot> snapshots = await products.GetAllSnapshotsAsync(ct);
        IReadOnlyList<SalesReportRow> allSales = await sales.GetSalesAsync(null, null, ct);
        IReadOnlyList<ProductLastSale> lastSales = await sales.GetLastSaleDatesAsync(ct);
        IReadOnlyList<ExpenseReportRow> allExpenses = await expenses.GetExpensesAsync(null, null, ct);
        IReadOnlyList<RecentSaleInfo> recentSales = await sales.GetRecentSalesAsync(RecentCount, ct);
        IReadOnlyList<RecentPaymentInfo> recentPayments = await customers.GetRecentPaymentsAsync(RecentCount, ct);
        decimal totalCustomerDebt = await customers.GetTotalDebtAsync(ct);
        decimal totalSupplierDebt = await suppliers.GetTotalDebtAsync(ct);
        ClosingSnapshot? lastClosing = await dayEnd.GetLastClosingAsync(ct);

        // Resolve the names of customers on recent credit sales in one query, so the feed can label them.
        IEnumerable<Guid> customerIds = recentSales
            .Where(s => s.CustomerId is not null)
            .Select(s => s.CustomerId!.Value);
        Dictionary<Guid, string> customerNames = await customers.GetNamesAsync(customerIds, ct);

        return DashboardCalculator.Build(
            snapshots,
            allSales,
            lastSales,
            allExpenses,
            recentSales,
            recentPayments,
            customerNames,
            totalCustomerDebt,
            totalSupplierDebt,
            lastClosing,
            today);
    }
}
