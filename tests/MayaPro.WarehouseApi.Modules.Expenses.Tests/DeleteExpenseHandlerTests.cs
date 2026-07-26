using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.DeleteExpense;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>
/// Unit tests for <see cref="DeleteExpenseHandler"/> around <c>source</c>: deleting a general expense must
/// not try to unwind a product cost (the third and last entry point into the maya chain), while deleting a
/// product expense still unwinds exactly the line it added.
/// </summary>
public sealed class DeleteExpenseHandlerTests
{
    private static readonly DateTime ExpenseDate = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Deleting_A_General_Expense_Never_Touches_Any_Product()
    {
        await using ExpensesDbContext db = TestDb.New();
        Expense expense = await SeedAsync(db, ExpenseSource.General, null, "Mağaza xərci", 600m);
        var products = new RecordingProductsModule();

        var result = await NewHandler(db, products).Handle(expense.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Empty(products.Removed);
        Assert.Equal(0, await db.Expenses.CountAsync());
    }

    [Fact]
    public async Task Deleting_A_Product_Expense_Unwinds_Exactly_Its_Own_Line()
    {
        await using ExpensesDbContext db = TestDb.New();
        Guid productId = Guid.NewGuid();
        Expense expense = await SeedAsync(db, ExpenseSource.Product, productId, "Yol pulu", 30m);
        var products = new RecordingProductsModule(productId);

        var result = await NewHandler(db, products).Handle(expense.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Equal((productId, "Yol pulu", 30m), Assert.Single(products.Removed));
        Assert.Equal(0, await db.Expenses.CountAsync());
    }

    private static async Task<Expense> SeedAsync(
        ExpensesDbContext db, ExpenseSource source, Guid? productId, string category, decimal amount)
    {
        var expense = Expense.Create(
            "Mövcud xərc",
            category,
            source,
            amount,
            ExpenseDate,
            productId,
            productId is null ? null : "Test malı",
            note: null,
            createdByUserId: null);

        db.Expenses.Add(expense);
        await db.SaveChangesAsync();
        return expense;
    }

    private static DeleteExpenseHandler NewHandler(ExpensesDbContext db, RecordingProductsModule products) =>
        new(db,
            new FakeUnitOfWork(db),
            products,
            new FakeDayEndModule(),
            new FakeActivityLogger(),
            new FakeCurrentUser(Guid.NewGuid()),
            new FakeDateProvider());
}
