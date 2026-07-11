using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDashboard;

/// <summary>
/// Builds the dashboard from the other modules' read contracts: stock value / low stock / frozen stock
/// from Products + Sales, today's revenue/profit from Sales, spend from Expenses, receivables from
/// Customers, payables from Suppliers, and expected drawer cash anchored to the last day-end closing.
/// The Reports module owns no tables — this is the whole point of the read-only design.
/// </summary>
public sealed class GetDashboardHandler(
    IProductsModule products,
    ISalesModule sales,
    IExpensesModule expenses,
    ICustomersModule customers,
    ISuppliersModule suppliers,
    IDayEndModule dayEnd)
{
    private const int TopProductsCount = 5;

    public async Task<DashboardDto> Handle(DateOnly today, CancellationToken ct)
    {
        IReadOnlyList<ProductSnapshot> snapshots = await products.GetAllSnapshotsAsync(ct);
        IReadOnlyList<SalesReportRow> allSales = await sales.GetSalesAsync(null, null, ct);
        IReadOnlyList<ProductLastSale> lastSales = await sales.GetLastSaleDatesAsync(ct);
        decimal totalCustomerDebt = await customers.GetTotalDebtAsync(ct);
        decimal totalSupplierDebt = await suppliers.GetTotalDebtAsync(ct);
        ClosingSnapshot? lastClosing = await dayEnd.GetLastClosingAsync(ct);

        List<SalesReportRow> todaySales = allSales.Where(s => s.Date == today).ToList();

        // Expected cash in the drawer: the cash counted at the last close, plus cash sales and minus
        // expenses booked since then. With no prior close we sum from the beginning of time.
        DateOnly? sinceInclusive = lastClosing is null ? null : lastClosing.Date.AddDays(1);
        decimal openingCash = lastClosing?.ActualCash ?? 0m;
        decimal cashSince = allSales
            .Where(s => s.PaymentType == WireFormat.PaymentTypes.Cash && (sinceInclusive is null || s.Date >= sinceInclusive))
            .Sum(s => s.TotalAmount);
        decimal expensesSince = (await expenses.GetExpensesAsync(sinceInclusive, today, ct)).Sum(e => e.Amount);
        decimal expectedCash = openingCash + cashSince - expensesSince;

        return new DashboardDto(
            ProductCount: snapshots.Count,
            LowStockCount: snapshots.Count(IsLowStock),
            OutOfStockCount: snapshots.Count(p => p.Quantity <= 0),
            StockCostValue: snapshots.Sum(p => p.Quantity * p.RealCostPerUnit),
            StockRetailValue: snapshots.Sum(p => p.Quantity * p.SalePrice),
            TodaySales: todaySales.Sum(s => s.TotalAmount),
            TodayProfit: todaySales.Sum(s => s.Profit),
            TodayExpenses: (await expenses.GetExpensesAsync(today, today, ct)).Sum(e => e.Amount),
            TodaySalesCount: todaySales.Count,
            TotalCustomerDebt: totalCustomerDebt,
            TotalSupplierDebt: totalSupplierDebt,
            ExpectedCash: expectedCash,
            FrozenProducts: BuildFrozen(snapshots, lastSales, today),
            TopProducts: BuildTopProducts(allSales),
            LowStock: snapshots
                .Where(IsLowStock)
                .OrderBy(p => p.Quantity)
                .Select(p => new LowStockProductDto(p.Id, p.Name, p.Quantity, p.MinStock))
                .ToList());
    }

    private static bool IsLowStock(ProductSnapshot p) => p.Quantity > 0 && p.Quantity <= p.MinStock;

    private static FrozenProductsDto BuildFrozen(
        IReadOnlyList<ProductSnapshot> snapshots,
        IReadOnlyList<ProductLastSale> lastSales,
        DateOnly today)
    {
        Dictionary<Guid, DateOnly> lastByProduct = lastSales.ToDictionary(x => x.ProductId, x => x.LastSale);

        int days30 = 0, days60 = 0, days90 = 0;
        foreach (ProductSnapshot p in snapshots.Where(p => p.Quantity > 0))
        {
            // Never-sold in-stock products are the most frozen of all → treated as effectively infinite age.
            int idleDays = lastByProduct.TryGetValue(p.Id, out DateOnly last)
                ? today.DayNumber - last.DayNumber
                : int.MaxValue;

            if (idleDays >= 30) days30++;
            if (idleDays >= 60) days60++;
            if (idleDays >= 90) days90++;
        }

        return new FrozenProductsDto(days30, days60, days90);
    }

    private static IReadOnlyList<TopProductDto> BuildTopProducts(IReadOnlyList<SalesReportRow> allSales) =>
        allSales
            .GroupBy(s => new { s.ProductId, s.ProductName })
            .Select(g => new TopProductDto(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Sum(s => s.Quantity),
                g.Sum(s => s.TotalAmount)))
            .OrderByDescending(t => t.QuantitySold)
            .ThenByDescending(t => t.Revenue)
            .Take(TopProductsCount)
            .ToList();
}
