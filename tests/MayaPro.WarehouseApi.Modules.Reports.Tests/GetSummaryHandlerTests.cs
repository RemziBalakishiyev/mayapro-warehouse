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

    private static ExpenseReportRow Expense(DateOnly date, decimal amount, string source) =>
        new(date, "Yol pulu", amount, source);

    private static SalesReportRow Sale(DateOnly date, decimal total, decimal? profit) =>
        new(date, total, profit, WireFormat.PaymentTypes.Cash, null, "P", 1, total, IsManual: false);

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
