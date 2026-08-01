using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSummary;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Tests;

/// <summary>
/// Unit tests for <see cref="GetSummaryHandler"/>'s expense split (AC-8 / TC-10): <c>generalExpenses</c> +
/// <c>productExpenses</c> is exactly the existing <c>expenses</c> total (no regression), <c>netProfit</c>
/// still uses that single total, and the period window is the one handed to the Expenses contract.
/// </summary>
public sealed class GetSummaryHandlerTests
{
    private static readonly DateOnly Today = new(2026, 7, 27);
    private const string General = WireFormat.ExpenseSources.General;
    private const string Product = WireFormat.ExpenseSources.Product;

    [Fact]
    public async Task Splits_Expenses_By_Source_And_The_Split_Sums_To_The_Total()
    {
        var expenses = new FakeExpensesModule(
            Expense(Today, 100m, General),
            Expense(Today, 250m, Product),
            Expense(Today, 50m, General));
        var handler = new GetSummaryHandler(
            new FakeSalesModule(Sale(Today, total: 1000m, profit: 400m)), expenses, new FixedDateProvider(Today));

        Result<SummaryDto> result = await handler.Handle("today", default);

        Assert.True(result.IsSuccess);
        SummaryDto summary = result.Value;
        Assert.Equal(150m, summary.GeneralExpenses);
        Assert.Equal(250m, summary.ProductExpenses);
        Assert.Equal(400m, summary.Expenses);                                        // unchanged total
        Assert.Equal(summary.Expenses, summary.GeneralExpenses + summary.ProductExpenses);
        Assert.Equal(0m, summary.NetProfit);                                         // 400 profit − 400 expenses
    }

    [Fact]
    public async Task Split_Is_Zero_When_There_Are_No_Expenses()
    {
        var handler = new GetSummaryHandler(
            new FakeSalesModule(), new FakeExpensesModule(), new FixedDateProvider(Today));

        Result<SummaryDto> result = await handler.Handle("today", default);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.GeneralExpenses);
        Assert.Equal(0m, result.Value.ProductExpenses);
        Assert.Equal(0m, result.Value.Expenses);
    }

    [Fact]
    public async Task Only_General_Expenses_Leaves_ProductExpenses_At_Zero()
    {
        var handler = new GetSummaryHandler(
            new FakeSalesModule(),
            new FakeExpensesModule(Expense(Today, 75m, General)),
            new FixedDateProvider(Today));

        Result<SummaryDto> result = await handler.Handle("today", default);

        Assert.True(result.IsSuccess);
        Assert.Equal(75m, result.Value.GeneralExpenses);
        Assert.Equal(0m, result.Value.ProductExpenses);
        Assert.Equal(75m, result.Value.Expenses);
    }

    [Fact]
    public async Task Expenses_Outside_The_Period_Are_Excluded_From_Both_The_Split_And_The_Total()
    {
        // Yesterday's expense must reach neither side of the split nor the total of a "today" request.
        DateOnly yesterday = Today.AddDays(-1);
        var expenses = new FakeExpensesModule(
            Expense(Today, 100m, General),
            Expense(yesterday, 500m, Product));
        var handler = new GetSummaryHandler(new FakeSalesModule(), expenses, new FixedDateProvider(Today));

        Result<SummaryDto> result = await handler.Handle("today", default);

        Assert.True(result.IsSuccess);
        SummaryDto summary = result.Value;
        Assert.Equal(100m, summary.GeneralExpenses);
        Assert.Equal(0m, summary.ProductExpenses);
        Assert.Equal(100m, summary.Expenses);
        Assert.Equal(summary.Expenses, summary.GeneralExpenses + summary.ProductExpenses);
    }

    [Fact]
    public async Task Only_Product_Expenses_Leaves_GeneralExpenses_At_Zero()
    {
        var expenses = new FakeExpensesModule(
            Expense(Today, 40m, Product),
            Expense(Today, 60m, Product));
        var handler = new GetSummaryHandler(new FakeSalesModule(), expenses, new FixedDateProvider(Today));

        Result<SummaryDto> result = await handler.Handle("today", default);

        Assert.True(result.IsSuccess);
        SummaryDto summary = result.Value;
        Assert.Equal(0m, summary.GeneralExpenses);
        Assert.Equal(100m, summary.ProductExpenses);
        Assert.Equal(100m, summary.Expenses);
        Assert.Equal(summary.Expenses, summary.GeneralExpenses + summary.ProductExpenses);
    }

    [Fact]
    public async Task A_Source_Outside_The_Known_Vocabulary_Still_Keeps_The_Split_Equal_To_The_Total()
    {
        // The split must never lose money: a row whose source is not "product" counts as general, so
        // general + product stays exactly the total even if the source vocabulary ever grows.
        var expenses = new FakeExpensesModule(
            Expense(Today, 30m, Product),
            Expense(Today, 70m, "supplier"));
        var handler = new GetSummaryHandler(new FakeSalesModule(), expenses, new FixedDateProvider(Today));

        Result<SummaryDto> result = await handler.Handle("today", default);

        Assert.True(result.IsSuccess);
        SummaryDto summary = result.Value;
        Assert.Equal(30m, summary.ProductExpenses);
        Assert.Equal(100m, summary.Expenses);
        Assert.Equal(summary.Expenses, summary.GeneralExpenses + summary.ProductExpenses);
    }

    [Fact]
    public async Task The_Period_Window_Is_The_One_Passed_To_The_Expenses_Contract()
    {
        // The split must be computed over the requested period, not over everything.
        var expenses = new FakeExpensesModule(Expense(Today, 10m, General));
        var handler = new GetSummaryHandler(new FakeSalesModule(), expenses, new FixedDateProvider(Today));

        Result<SummaryDto> result = await handler.Handle("month", default);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 7, 1), expenses.RequestedFrom);
        Assert.Equal(Today, expenses.RequestedTo);
        Assert.Equal("month", result.Value.Period);
    }

    /// <summary>
    /// BE#19: Cash/Card/Credit must be the "real money received" split (<see cref="SalesReportRowTotals"/>),
    /// not the pre-BE#15 "TotalAmount of sales whose PaymentType is X" formula — same repro numbers as
    /// <c>SalesModuleContractTests.TC12_Mixed_Day_Splits_Cash_Card_And_Credit_Correctly</c> and
    /// <c>SalesModuleContractTests.TC_Reports_Path_Matches_The_Day_End_Path_For_The_Same_Mixed_Day</c>, which
    /// compares this same split directly against <c>SalesModuleContract.GetDayTotalsAsync</c>.
    /// </summary>
    [Fact]
    public async Task BE19_Mixed_Day_Cash_Card_Credit_Is_The_Real_Received_Split()
    {
        var sales = new FakeSalesModule(
            Sale(Today, total: 200m, profit: 50m, WireFormat.PaymentTypes.Cash, paidAmount: 200m, paidVia: WireFormat.PaymentTypes.Cash),
            Sale(Today, total: 150m, profit: 30m, WireFormat.PaymentTypes.Card, paidAmount: 150m, paidVia: WireFormat.PaymentTypes.Card),
            Sale(Today, total: 500m, profit: 100m, WireFormat.PaymentTypes.Credit, paidAmount: 300m, paidVia: WireFormat.PaymentTypes.Cash),
            Sale(Today, total: 100m, profit: 20m, WireFormat.PaymentTypes.Credit, paidAmount: 0m, paidVia: null));
        var handler = new GetSummaryHandler(sales, new FakeExpensesModule(), new FixedDateProvider(Today));

        Result<SummaryDto> result = await handler.Handle("today", default);

        Assert.True(result.IsSuccess);
        SummaryDto summary = result.Value;
        Assert.Equal(500m, summary.CashSales);   // 200 (Nağd) + 300 (Nisyə's Nağd down-payment)
        Assert.Equal(150m, summary.CardSales);
        Assert.Equal(300m, summary.CreditSales); // 200 + 100 remaining, never the 600 full totals
        Assert.Equal(950m, summary.SalesTotal);  // unaffected — still the plain sum of every row's total
    }

    [Fact]
    public async Task BE19_A_Period_Without_Sales_Reports_Zero_Cash_Card_And_Credit()
    {
        // TC7: no rows at all must be zeros rather than a throw or a null — the summary is still a valid
        // (empty) report, which is the state every store starts its day in.
        var handler = new GetSummaryHandler(
            new FakeSalesModule(), new FakeExpensesModule(), new FixedDateProvider(Today));

        Result<SummaryDto> result = await handler.Handle("today", default);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.CashSales);
        Assert.Equal(0m, result.Value.CardSales);
        Assert.Equal(0m, result.Value.CreditSales);
        Assert.Equal(0, result.Value.SalesCount);
    }

    private static ExpenseReportRow Expense(DateOnly date, decimal amount, string source) =>
        new(date, "Yol pulu", amount, source);

    private static SalesReportRow Sale(DateOnly date, decimal total, decimal? profit) =>
        new(date, total, profit, WireFormat.PaymentTypes.Cash, null, "P", 1, total, IsManual: false);

    private static SalesReportRow Sale(
        DateOnly date, decimal total, decimal? profit, string paymentType, decimal? paidAmount, string? paidVia) =>
        new(date, total, profit, paymentType, null, "P", 1, total, IsManual: false, paidAmount, paidVia);

    private sealed class FakeExpensesModule(params ExpenseReportRow[] rows) : IExpensesModule
    {
        public DateOnly? RequestedFrom { get; private set; }

        public DateOnly? RequestedTo { get; private set; }

        public Task<decimal> GetDayTotalAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(rows.Where(r => r.Date == date).Sum(r => r.Amount));

        public Task<IReadOnlyList<ExpenseReportRow>> GetExpensesAsync(
            DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
        {
            RequestedFrom = from;
            RequestedTo = to;
            IReadOnlyList<ExpenseReportRow> window = rows
                .Where(r => (from is null || r.Date >= from) && (to is null || r.Date <= to))
                .ToList();
            return Task.FromResult(window);
        }
    }

    private sealed class FakeSalesModule(params SalesReportRow[] rows) : ISalesModule
    {
        public Task<IReadOnlyList<SalesReportRow>> GetSalesAsync(
            DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SalesReportRow> window = rows
                .Where(r => (from is null || r.Date >= from) && (to is null || r.Date <= to))
                .ToList();
            return Task.FromResult(window);
        }

        public Task<SalesDayTotals> GetDayTotalsAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ProductLastSale>> GetLastSaleDatesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RecentSaleInfo>> GetRecentSalesAsync(int take, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CustomerPurchaseStats>> GetPurchaseStatsByCustomerAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<CustomerSaleEntry>> GetSalesByCustomerAsync(
            Guid customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<CustomerOutstandingSale>> GetOutstandingSalesAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> DeleteCreditSaleAsync(
            Guid saleId, Guid customerId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SaleInvoiceInfo?> GetInvoiceSaleAsync(Guid saleId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Guid?> GetSaleIdByInvoiceTokenAsync(string token, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedDateProvider(DateOnly today) : IDateProvider
    {
        public DateTime UtcNow => today.ToDateTime(TimeOnly.MinValue);

        public DateOnly Today => today;

        public DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(utc);

        public DateTime ToLocalDateTime(DateTime utc) => utc;

        public (DateTime StartUtc, DateTime EndUtc) LocalDayRangeUtc(DateOnly localDate) =>
            (localDate.ToDateTime(TimeOnly.MinValue), localDate.AddDays(1).ToDateTime(TimeOnly.MinValue));
    }
}
