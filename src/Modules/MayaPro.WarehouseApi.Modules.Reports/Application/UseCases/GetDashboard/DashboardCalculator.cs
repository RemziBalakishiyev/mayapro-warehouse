using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDashboard;

/// <summary>
/// Pure dashboard maths. Takes the raw data already fetched from the other modules (all dates are the
/// business-zone local dates) plus "today", and produces the finished <see cref="DashboardDto"/>. Being
/// side-effect free, it is fully unit-testable without touching a database or mocking contracts.
/// </summary>
public static class DashboardCalculator
{
    private const int TopProductsCount = 5;
    private const int FrozenDays = 30;
    private const int DailySeriesDays = 14;
    private const int MonthlySeriesMonths = 6;

    public static DashboardDto Build(
        IReadOnlyList<ProductSnapshot> snapshots,
        IReadOnlyList<SalesReportRow> allSales,
        IReadOnlyList<ProductLastSale> lastSales,
        IReadOnlyList<ExpenseReportRow> allExpenses,
        IReadOnlyList<RecentSaleInfo> recentSales,
        IReadOnlyList<RecentPaymentInfo> recentPayments,
        IReadOnlyDictionary<Guid, string> customerNames,
        decimal totalCustomerDebt,
        decimal totalSupplierDebt,
        ClosingSnapshot? lastClosing,
        DateOnly today)
    {
        List<SalesReportRow> todaySales = allSales.Where(s => s.Date == today).ToList();

        return new DashboardDto(
            ProductCount: snapshots.Count,
            LowStockCount: snapshots.Count(IsLowStock),
            OutOfStockCount: snapshots.Count(p => p.Quantity <= 0),
            StockCostValue: snapshots.Sum(p => p.Quantity * p.RealCostPerUnit),
            StockRetailValue: snapshots.Sum(p => p.Quantity * p.SalePrice),
            TodaySales: todaySales.Sum(s => s.TotalAmount),
            // Sales whose cost is unknown (free-form sales without a cost) carry a null profit. They are
            // excluded from the profit total rather than counted as zero, and surfaced separately below so
            // the frontend can say "N of today's sales have unknown profit".
            TodayProfit: todaySales.Sum(s => s.Profit ?? 0m),
            UnknownProfitSalesCount: todaySales.Count(s => s.Profit is null),
            UnknownProfitAmount: todaySales.Where(s => s.Profit is null).Sum(s => s.TotalAmount),
            TodayExpenses: allExpenses.Where(e => e.Date == today).Sum(e => e.Amount),
            TodaySalesCount: todaySales.Count,
            TotalCustomerDebt: totalCustomerDebt,
            TotalSupplierDebt: totalSupplierDebt,
            ExpectedCash: ExpectedCash(allSales, allExpenses, lastClosing, today),
            FrozenProducts: BuildFrozen(snapshots, lastSales, today),
            TopProducts: BuildTopProducts(allSales),
            LowStock: snapshots
                .Where(IsLowStock)
                .OrderBy(p => p.Quantity)
                .Select(p => new LowStockProductDto(p.Id, p.Name, p.Quantity, p.MinStock))
                .ToList(),
            DailySeries: BuildDailySeries(allSales, today),
            MonthlySeries: BuildMonthlySeries(allSales, today),
            RecentSales: recentSales
                .Select(s => new RecentSaleDto(
                    s.Id, s.Date, s.ProductName, s.Category, s.Quantity, s.TotalAmount, s.PaymentType,
                    s.CustomerId is { } cid && customerNames.TryGetValue(cid, out string? name) ? name : null))
                .ToList(),
            RecentPayments: recentPayments
                .Select(p => new RecentPaymentDto(p.Id, p.Date, p.CustomerName, p.Amount))
                .ToList());
    }

    private static bool IsLowStock(ProductSnapshot p) => p.Quantity > 0 && p.Quantity <= p.MinStock;

    /// <summary>
    /// Expected cash in the drawer: the cash counted at the last close, plus cash sales and minus expenses
    /// booked since then. With no prior close we sum from the beginning of time.
    /// </summary>
    private static decimal ExpectedCash(
        IReadOnlyList<SalesReportRow> allSales,
        IReadOnlyList<ExpenseReportRow> allExpenses,
        ClosingSnapshot? lastClosing,
        DateOnly today)
    {
        DateOnly? since = lastClosing is null ? null : lastClosing.Date.AddDays(1);
        decimal openingCash = lastClosing?.ActualCash ?? 0m;

        decimal cashSince = allSales
            .Where(s => s.PaymentType == WireFormat.PaymentTypes.Cash && (since is null || s.Date >= since))
            .Sum(s => s.TotalAmount);
        decimal expensesSince = allExpenses
            .Where(e => (since is null || e.Date >= since) && e.Date <= today)
            .Sum(e => e.Amount);

        return openingCash + cashSince - expensesSince;
    }

    private static FrozenProductsDto BuildFrozen(
        IReadOnlyList<ProductSnapshot> snapshots,
        IReadOnlyList<ProductLastSale> lastSales,
        DateOnly today)
    {
        Dictionary<Guid, DateOnly> lastByProduct = lastSales.ToDictionary(x => x.ProductId, x => x.LastSale);

        int days30 = 0, days60 = 0, days90 = 0;
        var items = new List<FrozenProductDto>();

        foreach (ProductSnapshot p in snapshots.Where(p => p.Quantity > 0))
        {
            // Never-sold in-stock products are the most frozen of all → null idle days, most-frozen sort.
            int? idleDays = lastByProduct.TryGetValue(p.Id, out DateOnly last)
                ? today.DayNumber - last.DayNumber
                : null;
            int idleForBuckets = idleDays ?? int.MaxValue;

            if (idleForBuckets >= 30) days30++;
            if (idleForBuckets >= 60) days60++;
            if (idleForBuckets >= 90) days90++;

            if (idleForBuckets >= FrozenDays)
                items.Add(new FrozenProductDto(p.Id, p.Name, p.Quantity, p.Quantity * p.RealCostPerUnit, idleDays));
        }

        // Most-frozen first: never-sold (null) at the top, then by descending idle days.
        items = items
            .OrderByDescending(i => i.DaysSinceLastSale ?? int.MaxValue)
            .ThenByDescending(i => i.FrozenValue)
            .ToList();

        return new FrozenProductsDto(days30, days60, days90, items);
    }

    private static IReadOnlyList<TopProductDto> BuildTopProducts(IReadOnlyList<SalesReportRow> allSales) =>
        allSales
            // Free-form sales have no product, so they don't belong in a per-product ranking.
            .Where(s => s.ProductId is not null)
            .GroupBy(s => new { ProductId = s.ProductId!.Value, s.ProductName })
            .Select(g => new TopProductDto(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Sum(s => s.Quantity),
                g.Sum(s => s.TotalAmount)))
            .OrderByDescending(t => t.QuantitySold)
            .ThenByDescending(t => t.Revenue)
            .Take(TopProductsCount)
            .ToList();

    private static IReadOnlyList<DailyPointDto> BuildDailySeries(
        IReadOnlyList<SalesReportRow> allSales,
        DateOnly today)
    {
        var byDay = allSales
            .GroupBy(s => s.Date)
            // Unknown-profit sales still count toward sales, but not toward profit (null → excluded, not zero-counted).
            .ToDictionary(g => g.Key, g => (Sales: g.Sum(s => s.TotalAmount), Profit: g.Sum(s => s.Profit ?? 0m)));

        var series = new List<DailyPointDto>(DailySeriesDays);
        for (int i = DailySeriesDays - 1; i >= 0; i--)
        {
            DateOnly day = today.AddDays(-i);
            (decimal sales, decimal profit) = byDay.TryGetValue(day, out var v) ? v : (0m, 0m);
            series.Add(new DailyPointDto(day, sales, profit));
        }

        return series;
    }

    private static IReadOnlyList<MonthlyPointDto> BuildMonthlySeries(
        IReadOnlyList<SalesReportRow> allSales,
        DateOnly today)
    {
        var byMonth = allSales
            .GroupBy(s => new { s.Date.Year, s.Date.Month })
            .ToDictionary(g => (g.Key.Year, g.Key.Month), g => g.Sum(s => s.Profit ?? 0m));

        DateOnly firstOfThisMonth = new(today.Year, today.Month, 1);
        var series = new List<MonthlyPointDto>(MonthlySeriesMonths);
        for (int i = MonthlySeriesMonths - 1; i >= 0; i--)
        {
            DateOnly month = firstOfThisMonth.AddMonths(-i);
            decimal profit = byMonth.TryGetValue((month.Year, month.Month), out decimal p) ? p : 0m;
            series.Add(new MonthlyPointDto($"{month.Year:D4}-{month.Month:D2}", profit));
        }

        return series;
    }
}
