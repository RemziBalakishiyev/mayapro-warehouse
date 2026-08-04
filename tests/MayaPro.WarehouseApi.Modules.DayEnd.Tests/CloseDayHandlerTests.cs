using MayaPro.WarehouseApi.Modules.DayEnd.Application.Contracts;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.UseCases.CloseDay;
using MayaPro.WarehouseApi.Modules.DayEnd.Domain;
using MayaPro.WarehouseApi.Modules.DayEnd.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Tests;

/// <summary>
/// Unit tests for <see cref="CloseDayHandler"/>'s BE#33 salary breakdown: <c>ClosingDto.SalaryExpenses</c>
/// is exactly the salary payments <see cref="ISalaryModule.GetDayPaymentsTotalAsync"/> reports, and it never
/// changes the pre-existing <c>Expenses</c>/<c>ExpectedCash</c> arithmetic (BE#28, unaffected).
/// </summary>
public sealed class CloseDayHandlerTests
{
    private static readonly DateOnly Today = new(2026, 8, 3);

    [Fact]
    public async Task Close_Day_Stores_SalaryExpenses_Without_Changing_Expenses_Or_ExpectedCash()
    {
        await using DayEndDbContext db = NewDb();
        CloseDayHandler handler = NewHandler(
            db,
            sales: new FakeSalesModule(new SalesDayTotals(Cash: 200m, Card: 50m, Credit: 30m)),
            expenses: new FakeExpensesModule(40m),
            salary: new FakeSalaryModule(200m));

        Result<ClosingDto> result = await handler.Handle(
            new CloseDayCommand(OpeningCash: 100m, ActualCash: 90m, Note: "Gün sonu"), default);

        Assert.True(result.IsSuccess);
        ClosingDto closing = result.Value;
        Assert.Equal(200m, closing.SalaryExpenses);         // BE#33: broken out on its own
        Assert.Equal(240m, closing.Expenses);                // BE#28 behaviour unchanged: 40 + 200
        Assert.Equal(100m + 200m - 240m, closing.ExpectedCash); // 60 — unaffected by the new field
    }

    [Fact]
    public async Task Close_Day_With_No_Salary_Payments_Reports_Zero_SalaryExpenses()
    {
        await using DayEndDbContext db = NewDb();
        CloseDayHandler handler = NewHandler(
            db,
            sales: new FakeSalesModule(new SalesDayTotals(Cash: 100m, Card: 0m, Credit: 0m)),
            expenses: new FakeExpensesModule(30m),
            salary: new FakeSalaryModule(0m));

        Result<ClosingDto> result = await handler.Handle(
            new CloseDayCommand(OpeningCash: 0m, ActualCash: 70m, Note: null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.SalaryExpenses);
        Assert.Equal(30m, result.Value.Expenses); // store expense only — regression-safe
    }

    /// <summary>
    /// BE#33 AC (unit-level half of the preview-vs-closing consistency check — the HTTP-level half is
    /// <c>DayEndApiTests.Preview_And_Closing_Agree_On_Todays_Salary_Expenses</c>): both
    /// <c>GetSummaryHandler</c> and <c>CloseDayHandler</c> read <c>ISalaryModule</c>'s salary-payments total
    /// for "today" and pass it straight through with no extra maths, so a single fake total handed to both
    /// of <see cref="ISalaryModule"/>'s methods proves the two can only ever agree.
    /// </summary>
    [Fact]
    public async Task Closing_Reports_Exactly_The_Salary_Total_ISalaryModule_Provides_For_Today()
    {
        var salary = new FakeSalaryModule(150m);

        // What GetSummaryHandler's preview would read for "today" — same contract, same day.
        decimal previewSalaryExpenses = (await salary.GetPaymentsAsync(Today, Today, default)).Sum(p => p.Amount);

        await using DayEndDbContext db = NewDb();
        CloseDayHandler closeHandler = NewHandler(
            db,
            sales: new FakeSalesModule(new SalesDayTotals(Cash: 0m, Card: 0m, Credit: 0m)),
            expenses: new FakeExpensesModule(0m),
            salary: salary);
        Result<ClosingDto> closing = await closeHandler.Handle(
            new CloseDayCommand(OpeningCash: 0m, ActualCash: 0m, Note: null), default);

        Assert.True(closing.IsSuccess);
        Assert.Equal(previewSalaryExpenses, closing.Value.SalaryExpenses);
        Assert.Equal(150m, closing.Value.SalaryExpenses);
    }

    private static DayEndDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<DayEndDbContext>()
            .UseInMemoryDatabase($"dayend-tests-{Guid.NewGuid()}")
            .Options;
        return new DayEndDbContext(options);
    }

    private static CloseDayHandler NewHandler(
        DayEndDbContext db, ISalesModule sales, IExpensesModule expenses, ISalaryModule salary) =>
        new(
            db,
            new FakeUnitOfWork(db),
            sales,
            expenses,
            salary,
            new CloseDayValidator(),
            new FakeActivityLogger(),
            new FakeCurrentUser(),
            new FakeDateProvider());

    private sealed class FakeUnitOfWork(DayEndDbContext db) : IUnitOfWork
    {
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction(db));

        private sealed class FakeTransaction(DayEndDbContext db) : IUnitOfWorkTransaction
        {
            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
                db.SaveChangesAsync(cancellationToken);

            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FakeActivityLogger : IActivityLogger
    {
        public Task LogAsync(string type, string message, Guid? userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid? UserId => Guid.NewGuid();

        public string? Name => "Test sahibkar";

        public string? Role => WireFormat.Roles.Owner;

        public bool IsAuthenticated => true;
    }

    private sealed class FakeDateProvider : IDateProvider
    {
        public DateTime UtcNow => Today.ToDateTime(TimeOnly.MinValue);

        public DateOnly Today => CloseDayHandlerTests.Today;

        public DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(utc);

        public DateTime ToLocalDateTime(DateTime utc) => utc;

        public (DateTime StartUtc, DateTime EndUtc) LocalDayRangeUtc(DateOnly localDate) =>
            (localDate.ToDateTime(TimeOnly.MinValue), localDate.AddDays(1).ToDateTime(TimeOnly.MinValue));
    }

    private sealed class FakeSalesModule(SalesDayTotals totals) : ISalesModule
    {
        public Task<SalesDayTotals> GetDayTotalsAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(totals);

        public Task<IReadOnlyList<SalesReportRow>> GetSalesAsync(
            DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) =>
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

    private sealed class FakeExpensesModule(decimal dayTotal) : IExpensesModule
    {
        public Task<decimal> GetDayTotalAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(dayTotal);

        public Task<IReadOnlyList<ExpenseReportRow>> GetExpensesAsync(
            DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSalaryModule(decimal dayTotal) : ISalaryModule
    {
        public Task<decimal> GetDayPaymentsTotalAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(dayTotal);

        public Task<IReadOnlyList<SalaryPaymentRow>> GetPaymentsAsync(
            DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SalaryPaymentRow> rows = dayTotal == 0m
                ? Array.Empty<SalaryPaymentRow>()
                : [new SalaryPaymentRow(Today, Guid.NewGuid(), "Test işçi", dayTotal)];
            return Task.FromResult(rows);
        }
    }

}
