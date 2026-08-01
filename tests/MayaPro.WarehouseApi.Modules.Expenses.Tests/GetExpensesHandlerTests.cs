using MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.GetExpenses;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>
/// Unit tests for <see cref="GetExpensesHandler"/>'s <c>source</c> filter (TC-6, TC-7, TC-12) — combined
/// with the existing <c>month</c> filter and on its own — plus the <c>from</c>/<c>to</c> date-range filter
/// added for BE#25 (TC-1 .. TC-12).
/// </summary>
public sealed class GetExpensesHandlerTests
{
    private static readonly DateTime InMonth = new(2026, 7, 10);
    private static readonly DateTime OtherMonth = new(2026, 6, 10);

    [Fact]
    public async Task Source_General_Returns_Only_General_Expenses()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db,
            (ExpenseSource.General, null, InMonth),
            (ExpenseSource.General, null, InMonth),
            (ExpenseSource.Product, Guid.NewGuid(), InMonth),
            (ExpenseSource.Product, Guid.NewGuid(), InMonth),
            (ExpenseSource.Product, Guid.NewGuid(), InMonth));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: "2026-07", source: "general", from: null, to: null, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, e => Assert.Equal("general", e.Source));
    }

    [Fact]
    public async Task Source_Product_Returns_Only_Product_Expenses()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db,
            (ExpenseSource.General, null, InMonth),
            (ExpenseSource.General, null, InMonth),
            (ExpenseSource.Product, Guid.NewGuid(), InMonth),
            (ExpenseSource.Product, Guid.NewGuid(), InMonth),
            (ExpenseSource.Product, Guid.NewGuid(), InMonth));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: "2026-07", source: "product", from: null, to: null, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.All(result.Value, e => Assert.Equal("product", e.Source));
    }

    [Fact]
    public async Task Unknown_Source_Returns_Validation_Failure_Not_500()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db, (ExpenseSource.General, null, InMonth));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: null, source: "unknown", from: null, to: null, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ExpenseErrors.InvalidSource, result.Error);
    }

    [Fact]
    public async Task No_Source_Filter_Returns_Every_Expense()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db,
            (ExpenseSource.General, null, InMonth),
            (ExpenseSource.Product, Guid.NewGuid(), InMonth));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: null, source: null, from: null, to: null, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task Source_And_Month_Filters_Are_Applied_Together()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db,
            (ExpenseSource.General, null, InMonth),
            (ExpenseSource.General, null, OtherMonth),   // right source, wrong month
            (ExpenseSource.Product, Guid.NewGuid(), InMonth));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: "2026-07", source: "general", from: null, to: null, default);

        Assert.True(result.IsSuccess);
        ExpenseDto only = Assert.Single(result.Value);
        Assert.Equal("general", only.Source);
        Assert.Equal(InMonth, only.Date);
    }

    [Fact]
    public async Task Rows_Are_Newest_First_And_Carry_The_Category_Snapshot()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db,
            (ExpenseSource.General, null, InMonth),
            (ExpenseSource.General, null, InMonth.AddDays(5)));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: null, source: "general", from: null, to: null, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(InMonth.AddDays(5), result.Value[0].Date);
        Assert.Equal(InMonth, result.Value[1].Date);
        // Category is a free-form snapshot now, not a code — it must round-trip untouched.
        Assert.All(result.Value, e => Assert.Equal("Yol pulu", e.Category));
    }

    // --- BE#25: from/to date-range filter -----------------------------------------------------

    [Fact] // TC-1
    public async Task From_Only_Returns_Expenses_On_Or_After_The_Date()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db,
            (ExpenseSource.General, null, new DateTime(2019, 12, 31)),
            (ExpenseSource.General, null, new DateTime(2020, 1, 1)),
            (ExpenseSource.General, null, new DateTime(2020, 1, 15)));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: null, source: null, from: "2020-01-01", to: null, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, e => Assert.True(e.Date >= new DateTime(2020, 1, 1)));
    }

    [Fact] // TC-2
    public async Task To_Only_Returns_Expenses_On_Or_Before_The_Date()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db,
            (ExpenseSource.General, null, new DateTime(2019, 12, 31)),
            (ExpenseSource.General, null, new DateTime(2020, 1, 1)),
            (ExpenseSource.General, null, new DateTime(2020, 1, 15)));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: null, source: null, from: null, to: "2020-01-01", default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, e => Assert.True(e.Date <= new DateTime(2020, 1, 1)));
    }

    [Fact] // TC-3 + TC-4 (inclusive boundaries)
    public async Task From_And_To_Together_Filter_An_Inclusive_Range()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db,
            (ExpenseSource.General, null, new DateTime(2019, 12, 31)),
            (ExpenseSource.General, null, new DateTime(2020, 1, 1)),
            (ExpenseSource.General, null, new DateTime(2020, 1, 2)),
            (ExpenseSource.General, null, new DateTime(2020, 1, 3)));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: null, source: null, from: "2020-01-01", to: "2020-01-02", default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, e => e.Date == new DateTime(2020, 1, 1));
        Assert.Contains(result.Value, e => e.Date == new DateTime(2020, 1, 2));
    }

    [Fact] // TC-5 + AC-4
    public async Task From_And_To_Take_Priority_Over_Month_When_Both_Are_Sent()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db,
            (ExpenseSource.General, null, new DateTime(2020, 1, 1)),   // in from/to range, wrong month
            (ExpenseSource.General, null, new DateTime(2020, 2, 10))); // in month, out of from/to range
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: "2020-02", source: null, from: "2020-01-01", to: "2020-01-02", default);

        Assert.True(result.IsSuccess);
        ExpenseDto only = Assert.Single(result.Value);
        Assert.Equal(new DateTime(2020, 1, 1), only.Date);
    }

    [Fact] // TC-6 regression
    public async Task Month_Only_Still_Works_When_From_To_Are_Absent()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db,
            (ExpenseSource.General, null, InMonth),
            (ExpenseSource.General, null, OtherMonth));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: "2026-07", source: null, from: null, to: null, default);

        Assert.True(result.IsSuccess);
        ExpenseDto only = Assert.Single(result.Value);
        Assert.Equal(InMonth, only.Date);
    }

    [Theory] // TC-7 + TC-8
    [InlineData("2020/01/01", null)]
    [InlineData(null, "not-a-date")]
    [InlineData("01-01-2020", null)]
    public async Task Malformed_From_Or_To_Returns_InvalidDateRange(string? from, string? to)
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db, (ExpenseSource.General, null, InMonth));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: null, source: null, from, to, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ExpenseErrors.InvalidDateRange, result.Error);
    }

    [Fact] // TC-9
    public async Task From_After_To_Returns_InvalidDateRange()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db, (ExpenseSource.General, null, InMonth));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: null, source: null, from: "2020-02-01", to: "2020-01-01", default);

        Assert.True(result.IsFailure);
        Assert.Equal(ExpenseErrors.InvalidDateRange, result.Error);
    }

    [Fact] // TC-10 regression
    public async Task No_Filters_Returns_Every_Expense()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db,
            (ExpenseSource.General, null, InMonth),
            (ExpenseSource.Product, Guid.NewGuid(), OtherMonth));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(month: null, source: null, from: null, to: null, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact] // TC-11
    public async Task From_To_Combine_With_Source_Filter()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db,
            (ExpenseSource.General, null, new DateTime(2020, 1, 1)),
            (ExpenseSource.Product, Guid.NewGuid(), new DateTime(2020, 1, 1)),
            (ExpenseSource.General, null, new DateTime(2020, 1, 2)),
            (ExpenseSource.General, null, new DateTime(2019, 12, 1)));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(
            month: null, source: "general", from: "2020-01-01", to: "2020-01-02", default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, e => Assert.Equal("general", e.Source));
    }

    [Fact] // TC-12
    public async Task From_To_With_No_Matches_Returns_Empty_List_Not_An_Error()
    {
        await using ExpensesDbContext db = NewDb();
        Seed(db, (ExpenseSource.General, null, InMonth));
        await db.SaveChangesAsync();

        var handler = new GetExpensesHandler(db);
        var result = await handler.Handle(
            month: null, source: null, from: "2099-01-01", to: "2099-01-02", default);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    private static void Seed(ExpensesDbContext db, params (ExpenseSource Source, Guid? ProductId, DateTime Date)[] rows)
    {
        foreach (var row in rows)
        {
            var expense = Expense.Create(
                "Test xərci",
                "Yol pulu",
                row.Source,
                10m,
                row.Date,
                row.ProductId,
                row.ProductId is null ? null : "Test malı",
                note: null,
                createdByUserId: null);
            db.Expenses.Add(expense);
        }
    }

    private static ExpensesDbContext NewDb() => TestDb.New();
}
