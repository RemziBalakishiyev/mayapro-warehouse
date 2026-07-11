using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDashboard;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Tests;

/// <summary>Unit tests for the pure <see cref="DashboardCalculator"/> — all figures from in-memory inputs.</summary>
public sealed class DashboardCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 7, 12);
    private const string Cash = WireFormat.PaymentTypes.Cash;
    private const string Card = WireFormat.PaymentTypes.Card;

    private static DashboardDto Build(
        IReadOnlyList<ProductSnapshot>? snapshots = null,
        IReadOnlyList<SalesReportRow>? sales = null,
        IReadOnlyList<ProductLastSale>? lastSales = null,
        IReadOnlyList<ExpenseReportRow>? expenses = null,
        IReadOnlyList<RecentSaleInfo>? recentSales = null,
        IReadOnlyList<RecentPaymentInfo>? recentPayments = null,
        decimal customerDebt = 0m,
        decimal supplierDebt = 0m,
        ClosingSnapshot? lastClosing = null) =>
        DashboardCalculator.Build(
            snapshots ?? [],
            sales ?? [],
            lastSales ?? [],
            expenses ?? [],
            recentSales ?? [],
            recentPayments ?? [],
            customerDebt,
            supplierDebt,
            lastClosing,
            Today);

    private static ProductSnapshot Snap(Guid id, int qty, int min, decimal realCost, decimal salePrice) =>
        new(id, "P", "Cat", qty, min, realCost, salePrice);

    private static SalesReportRow Sale(DateOnly date, decimal total, decimal profit, int qty = 1, string? payment = null, Guid product = default, string name = "P") =>
        new(date, total, profit, payment ?? Cash, product, name, qty);

    private static ExpenseReportRow Exp(DateOnly date, decimal amount) => new(date, "Yol", amount);

    [Fact]
    public void Today_Aggregates_Only_Todays_Rows()
    {
        var dto = Build(
            sales:
            [
                Sale(Today, total: 30, profit: 15, qty: 3),
                Sale(Today, total: 20, profit: 8, qty: 2, payment: Card),
                Sale(Today.AddDays(-1), total: 10, profit: 4)
            ],
            expenses: [Exp(Today, 40), Exp(Today.AddDays(-1), 10)]);

        Assert.Equal(50m, dto.TodaySales);
        Assert.Equal(23m, dto.TodayProfit);
        Assert.Equal(40m, dto.TodayExpenses);
        Assert.Equal(2, dto.TodaySalesCount);
    }

    [Fact]
    public void DailySeries_Is_14_Days_Ending_Today_With_Zero_Fill()
    {
        var dto = Build(sales:
        [
            Sale(Today, total: 30, profit: 15),
            Sale(Today.AddDays(-13), total: 5, profit: 2),
            Sale(Today.AddDays(-20), total: 100, profit: 50)   // outside the window → ignored
        ]);

        Assert.Equal(14, dto.DailySeries.Count);
        Assert.Equal(Today.AddDays(-13), dto.DailySeries[0].Date);
        Assert.Equal(Today, dto.DailySeries[^1].Date);
        Assert.Equal(30m, dto.DailySeries[^1].Sales);
        Assert.Equal(2m, dto.DailySeries[0].Profit);
        Assert.Equal(0m, dto.DailySeries[5].Sales);          // a day with no sales
    }

    [Fact]
    public void MonthlySeries_Is_6_Months_Ending_This_Month()
    {
        var dto = Build(sales:
        [
            Sale(Today, total: 30, profit: 15),
            Sale(new DateOnly(2026, 6, 10), total: 40, profit: 20),  // last month
            Sale(new DateOnly(2025, 12, 1), total: 10, profit: 100)  // 7 months ago → outside
        ]);

        Assert.Equal(6, dto.MonthlySeries.Count);
        Assert.Equal("2026-02", dto.MonthlySeries[0].Month);
        Assert.Equal("2026-07", dto.MonthlySeries[^1].Month);
        Assert.Equal(15m, dto.MonthlySeries[^1].Profit);
        Assert.Equal(20m, dto.MonthlySeries[4].Profit);          // June
        Assert.DoesNotContain(dto.MonthlySeries, m => m.Month == "2025-12");
    }

    [Fact]
    public void Frozen_Buckets_Are_Cumulative_And_Items_Are_Detailed()
    {
        Guid a = Guid.NewGuid(), e = Guid.NewGuid(), f = Guid.NewGuid(), g = Guid.NewGuid(), outOfStock = Guid.NewGuid();
        var dto = Build(
            snapshots:
            [
                Snap(a, qty: 10, min: 2, realCost: 5, salePrice: 10),   // sold 5 days ago → not frozen
                Snap(e, qty: 4, min: 1, realCost: 2, salePrice: 6),     // never sold → most frozen
                Snap(f, qty: 5, min: 1, realCost: 3, salePrice: 8),     // sold 95 days ago
                Snap(g, qty: 2, min: 1, realCost: 1, salePrice: 4),     // sold 45 days ago
                Snap(outOfStock, qty: 0, min: 1, realCost: 1, salePrice: 4)
            ],
            lastSales:
            [
                new ProductLastSale(a, Today.AddDays(-5)),
                new ProductLastSale(f, Today.AddDays(-95)),
                new ProductLastSale(g, Today.AddDays(-45))
            ]);

        Assert.Equal(3, dto.FrozenProducts.Days30);  // e, f, g
        Assert.Equal(2, dto.FrozenProducts.Days60);  // e, f
        Assert.Equal(2, dto.FrozenProducts.Days90);  // e, f
        Assert.Equal(3, dto.FrozenProducts.Items.Count);

        FrozenProductDto first = dto.FrozenProducts.Items[0];
        Assert.Equal(e, first.Id);                    // never-sold is the most frozen
        Assert.Null(first.DaysSinceLastSale);
        Assert.Equal(8m, first.FrozenValue);          // 4 units × real cost 2

        FrozenProductDto ninetyFive = dto.FrozenProducts.Items.Single(i => i.Id == f);
        Assert.Equal(95, ninetyFive.DaysSinceLastSale);
        Assert.Equal(15m, ninetyFive.FrozenValue);
    }

    [Fact]
    public void ExpectedCash_Anchors_To_Last_Close_Then_Adds_Cash_Minus_Expenses()
    {
        var dto = Build(
            sales:
            [
                Sale(Today.AddDays(-5), total: 999, profit: 0),                 // before the close → excluded
                Sale(Today.AddDays(-1), total: 50, profit: 0),                  // cash since close
                Sale(Today, total: 30, profit: 0),                             // cash since close
                Sale(Today, total: 20, profit: 0, payment: Card)               // not cash → excluded
            ],
            expenses:
            [
                Exp(Today.AddDays(-5), 100),                                    // before the close → excluded
                Exp(Today.AddDays(-1), 10),
                Exp(Today, 5)
            ],
            lastClosing: new ClosingSnapshot(Today.AddDays(-2), ActualCash: 100));

        // 100 opening + (50 + 30) cash − (10 + 5) expenses = 165
        Assert.Equal(165m, dto.ExpectedCash);
    }

    [Fact]
    public void Top_Products_And_Low_Stock_Are_Ranked_And_Consistent()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), outOfStock = Guid.NewGuid();
        var dto = Build(
            snapshots:
            [
                Snap(a, qty: 10, min: 2, realCost: 5, salePrice: 10),  // not low (qty > min)
                Snap(b, qty: 1, min: 5, realCost: 3, salePrice: 8),    // low (0 < qty ≤ min)
                Snap(outOfStock, qty: 0, min: 1, realCost: 1, salePrice: 4)
            ],
            sales:
            [
                Sale(Today, total: 30, profit: 0, qty: 3, product: a),
                Sale(Today, total: 20, profit: 0, qty: 2, product: a),
                Sale(Today, total: 40, profit: 0, qty: 5, product: b)
            ]);

        Assert.Equal(1, dto.LowStockCount);
        Assert.Equal(1, dto.OutOfStockCount);
        Assert.Equal(b, Assert.Single(dto.LowStock).ProductId);

        // A sold 5 units / 50; B sold 5 units / 40 → tie on quantity broken by revenue → A first.
        Assert.Equal(2, dto.TopProducts.Count);
        Assert.Equal(a, dto.TopProducts[0].ProductId);
        Assert.Equal(5, dto.TopProducts[0].QuantitySold);
        Assert.Equal(50m, dto.TopProducts[0].Revenue);
    }

    [Fact]
    public void Recent_Sales_And_Payments_Are_Passed_Through()
    {
        Guid saleId = Guid.NewGuid(), payId = Guid.NewGuid();
        var dto = Build(
            recentSales: [new RecentSaleInfo(saleId, Today, "Şalvar", 2, 40m, Cash)],
            recentPayments: [new RecentPaymentInfo(payId, Today, "Əli", 25m)]);

        RecentSaleDto s = Assert.Single(dto.RecentSales);
        Assert.Equal(saleId, s.Id);
        Assert.Equal("Şalvar", s.ProductName);
        Assert.Equal(Cash, s.PaymentType);

        RecentPaymentDto p = Assert.Single(dto.RecentPayments);
        Assert.Equal(payId, p.Id);
        Assert.Equal("Əli", p.CustomerName);
        Assert.Equal(25m, p.Amount);
    }
}
