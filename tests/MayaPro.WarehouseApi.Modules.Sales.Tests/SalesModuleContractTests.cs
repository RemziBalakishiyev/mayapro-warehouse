using MayaPro.WarehouseApi.Modules.Sales.Application;
using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.DeleteSale;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using MayaPro.WarehouseApi.Modules.Sales.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Sales.Tests;

/// <summary>
/// Unit tests for <see cref="SalesModuleContract.GetDayTotalsAsync"/> — BE#15's "real cash received"
/// day-end aggregation: Cash/Card sum <see cref="Sale.PaidAmount"/> by <see cref="Sale.PaidVia"/> (which also
/// picks up a Nisyə sale's cash/card down-payment), Credit sums only what remains unpaid.
/// </summary>
public sealed class SalesModuleContractTests
{
    // A real clock (Asia/Baku, like production) so sales stamped with the real DateTime.UtcNow at Create
    // time reliably fall on "today" — GetDayTotalsAsync buckets by the business (Baku) day.
    private static readonly AppDateProvider DateProvider =
        new(AppDateProvider.ResolveTimeZone(null), () => DateTime.UtcNow);

    [Fact]
    public async Task TC12_Mixed_Day_Splits_Cash_Card_And_Credit_Correctly()
    {
        await using SalesDbContext db = NewDb();

        // Nağd 200 (fully paid) + Kart 150 (fully paid)
        db.Sales.Add(Manual(total: 200m, PaymentType.Cash, customerId: null));
        db.Sales.Add(Manual(total: 150m, PaymentType.Card, customerId: null));
        // Nisyə 500, paid 300 via Nağd → remaining 200
        db.Sales.Add(Manual(total: 500m, PaymentType.Credit, customerId: Guid.NewGuid(), paidAmount: 300m, paidVia: PaymentType.Cash));
        // Nisyə 100, paid 0 → remaining 100
        db.Sales.Add(Manual(total: 100m, PaymentType.Credit, customerId: Guid.NewGuid(), paidAmount: 0m));
        await db.SaveChangesAsync();

        SalesModuleContract contract = NewContract(db);

        SalesDayTotals totals = await contract.GetDayTotalsAsync(DateProvider.Today, default);

        Assert.Equal(500m, totals.Cash);   // 200 (Nağd) + 300 (Nisyə's Nağd down-payment)
        Assert.Equal(150m, totals.Card);
        Assert.Equal(300m, totals.Credit); // 200 + 100 remaining
    }

    /// <summary>
    /// BE#19: the Reports/Exports side (<see cref="SalesReportRowTotals.ComputeReceivedTotals"/>, applied to
    /// <see cref="SalesModuleContract.GetSalesAsync"/>'s rows) must land on exactly the same Cash/Card/Credit
    /// figures as the dashboard/day-end side (<see cref="SalesModuleContract.GetDayTotalsAsync"/>) for the
    /// identical mixed day — the two paths are compared directly here rather than pinned to the same
    /// hard-coded numbers independently, which is how BE#19's drift slipped through undetected.
    /// </summary>
    [Fact]
    public async Task TC_Reports_Path_Matches_The_Day_End_Path_For_The_Same_Mixed_Day()
    {
        await using SalesDbContext db = NewDb();

        db.Sales.Add(Manual(total: 200m, PaymentType.Cash, customerId: null));
        db.Sales.Add(Manual(total: 150m, PaymentType.Card, customerId: null));
        db.Sales.Add(Manual(total: 500m, PaymentType.Credit, customerId: Guid.NewGuid(), paidAmount: 300m, paidVia: PaymentType.Cash));
        db.Sales.Add(Manual(total: 100m, PaymentType.Credit, customerId: Guid.NewGuid(), paidAmount: 0m));
        await db.SaveChangesAsync();

        SalesModuleContract contract = NewContract(db);

        SalesDayTotals dayEndTotals = await contract.GetDayTotalsAsync(DateProvider.Today, default);
        IReadOnlyList<SalesReportRow> reportRows = await contract.GetSalesAsync(DateProvider.Today, DateProvider.Today, default);
        SalesDayTotals reportsTotals = reportRows.ComputeReceivedTotals();

        // Pinned figures (same as TC12) …
        Assert.Equal(500m, reportsTotals.Cash);
        Assert.Equal(150m, reportsTotals.Card);
        Assert.Equal(300m, reportsTotals.Credit);
        // … and, the point of this test, identical to the dashboard/day-end result for the same day.
        Assert.Equal(dayEndTotals.Cash, reportsTotals.Cash);
        Assert.Equal(dayEndTotals.Card, reportsTotals.Card);
        Assert.Equal(dayEndTotals.Credit, reportsTotals.Credit);
    }

    [Fact]
    public async Task Sales_Outside_The_Requested_Day_Are_Excluded()
    {
        await using SalesDbContext db = NewDb();
        db.Sales.Add(Manual(total: 999m, PaymentType.Cash, customerId: null));
        await db.SaveChangesAsync();

        // Force the row's Date outside "today" by asking about a day far in the past.
        SalesModuleContract contract = NewContract(db);
        SalesDayTotals totals = await contract.GetDayTotalsAsync(new DateOnly(2000, 1, 1), default);

        Assert.Equal(0m, totals.Cash);
        Assert.Equal(0m, totals.Card);
        Assert.Equal(0m, totals.Credit);
    }

    /// <summary>A fully paid Nisyə edge case never happens via the handler (SalePaymentPlan always forces
    /// Credit when there is a remaining balance and never stores it otherwise), but the aggregation must
    /// still behave sanely if it ever did: nothing remains, so it contributes nothing to Credit.</summary>
    [Fact]
    public async Task Credit_Row_With_No_Remaining_Balance_Contributes_Nothing_To_Credit()
    {
        await using SalesDbContext db = NewDb();
        db.Sales.Add(Manual(total: 300m, PaymentType.Credit, customerId: Guid.NewGuid(), paidAmount: 300m, paidVia: PaymentType.Card));
        await db.SaveChangesAsync();

        SalesModuleContract contract = NewContract(db);
        SalesDayTotals totals = await contract.GetDayTotalsAsync(DateProvider.Today, default);

        Assert.Equal(0m, totals.Cash);
        Assert.Equal(300m, totals.Card); // the paid-in-full portion is still real card income
        Assert.Equal(0m, totals.Credit);
    }

    // ── QA (BE#15) — a deleted sale must stop counting toward the day's totals ─────────────────────────

    [Fact]
    public async Task Deleting_A_Sale_Removes_It_From_The_Days_Totals()
    {
        await using SalesDbContext db = NewDb();

        Sale cashSale = Manual(total: 200m, PaymentType.Cash, customerId: null);
        Sale cardSale = Manual(total: 150m, PaymentType.Card, customerId: null);
        db.Sales.Add(cashSale);
        db.Sales.Add(cardSale);
        await db.SaveChangesAsync();

        var deleteHandler = new DeleteSaleHandler(
            db,
            new FakeUnitOfWork(db),
            new UnusedProductsModule(),
            new UnusedCustomersModule(),
            new FakeActivityLogger(),
            new FakeCurrentUser());
        SalesModuleContract contract = new(db, DateProvider, deleteHandler);

        SalesDayTotals before = await contract.GetDayTotalsAsync(DateProvider.Today, default);
        Assert.Equal(200m, before.Cash);
        Assert.Equal(150m, before.Card);

        // The deleted sale is free-form (ProductId null) and non-credit (PaymentType.Cash), so neither
        // Unused collaborator is ever hit — nothing to reverse but the row itself.
        Result delete = await deleteHandler.Handle(cashSale.Id, default);
        Assert.True(delete.IsSuccess);

        SalesDayTotals after = await contract.GetDayTotalsAsync(DateProvider.Today, default);
        Assert.Equal(0m, after.Cash);    // the deleted sale no longer contributes
        Assert.Equal(150m, after.Card);  // the untouched sale still does
    }

    private static Sale Manual(
        decimal total, PaymentType paymentType, Guid? customerId, decimal? paidAmount = null, PaymentType? paidVia = null) =>
        Sale.CreateManual(
            productName: "Test malı",
            category: null,
            quantity: 1,
            unitPrice: total,
            costPerUnit: null,
            paymentType: paymentType,
            customerId: customerId,
            soldByUserId: null,
            soldByName: "Satıcı",
            paidAmount: paidAmount,
            paidVia: paidVia);

    private static SalesModuleContract NewContract(SalesDbContext db) =>
        new(
            db,
            DateProvider,
            new DeleteSaleHandler(
                db,
                new FakeUnitOfWork(db),
                new UnusedProductsModule(),
                new UnusedCustomersModule(),
                new FakeActivityLogger(),
                new FakeCurrentUser()));

    private static SalesDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-tests-{Guid.NewGuid()}")
            .Options;
        return new SalesDbContext(options);
    }
}
