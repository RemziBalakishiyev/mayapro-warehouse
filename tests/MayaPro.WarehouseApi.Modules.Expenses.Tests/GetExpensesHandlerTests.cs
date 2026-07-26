using MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.GetExpenses;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>
/// Unit tests for <see cref="GetExpensesHandler"/>'s <c>source</c> filter (TC-6, TC-7, TC-12) — combined
/// with the existing <c>month</c> filter and on its own.
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
        var result = await handler.Handle(month: "2026-07", source: "general", default);

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
        var result = await handler.Handle(month: "2026-07", source: "product", default);

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
        var result = await handler.Handle(month: null, source: "unknown", default);

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
        var result = await handler.Handle(month: null, source: null, default);

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
        var result = await handler.Handle(month: "2026-07", source: "general", default);

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
        var result = await handler.Handle(month: null, source: "general", default);

        Assert.True(result.IsSuccess);
        Assert.Equal(InMonth.AddDays(5), result.Value[0].Date);
        Assert.Equal(InMonth, result.Value[1].Date);
        // Category is a free-form snapshot now, not a code — it must round-trip untouched.
        Assert.All(result.Value, e => Assert.Equal("Yol pulu", e.Category));
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
