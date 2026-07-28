using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.UpdateExpense;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>
/// Unit tests for <see cref="UpdateExpenseHandler"/> around the new <c>source</c> field — the edit path is
/// the second way an expense can reach (or escape) the product real-cost chain, so switching an expense
/// between <c>general</c> and <c>product</c> must reverse and reapply exactly once, and a general expense
/// must still never touch maya.
/// </summary>
public sealed class UpdateExpenseHandlerTests
{
    private const string General = WireFormat.ExpenseSources.General;
    private const string Product = WireFormat.ExpenseSources.Product;
    private static readonly DateTime ExpenseDate = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Switching_Product_To_General_Reverses_The_Cost_And_Never_Reapplies_It()
    {
        await using ExpensesDbContext db = TestDb.New();
        Guid productId = Guid.NewGuid();
        Expense expense = await SeedAsync(db, ExpenseSource.Product, productId, "Yol pulu", 30m);
        var products = new RecordingProductsModule(productId);

        var result = await NewHandler(db, products).Handle(
            new UpdateExpenseCommand(
                expense.Id, "Mağaza icarəsi", "Mağaza xərci", General, 30m, null, null, null), default);

        Assert.True(result.IsSuccess);
        // Reversed with the OLD line name and amount…
        (Guid ProductId, string Category, decimal Amount) reversal = Assert.Single(products.Removed);
        Assert.Equal((productId, "Yol pulu", 30m), reversal);
        // …and nothing was re-applied: the expense is general now.
        Assert.Empty(products.Added);

        Expense stored = await db.Expenses.SingleAsync();
        Assert.Equal(ExpenseSource.General, stored.Source);
        Assert.Null(stored.ProductId);
        Assert.Null(stored.ProductName);
    }

    [Fact]
    public async Task Switching_General_To_Product_Applies_The_Cost_Once_And_Reverses_Nothing()
    {
        await using ExpensesDbContext db = TestDb.New();
        Guid productId = Guid.NewGuid();
        Expense expense = await SeedAsync(db, ExpenseSource.General, null, "Mağaza xərci", 40m);
        var products = new RecordingProductsModule(productId);

        var result = await NewHandler(db, products).Handle(
            new UpdateExpenseCommand(
                expense.Id, "Karqo", "Yol pulu", Product, 40m, null, productId, null), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(products.Removed); // there was no product effect to unwind
        Assert.Equal((productId, "Yol pulu", 40m), Assert.Single(products.Added));

        Expense stored = await db.Expenses.SingleAsync();
        Assert.Equal(ExpenseSource.Product, stored.Source);
        Assert.Equal(productId, stored.ProductId);
    }

    [Fact]
    public async Task Editing_A_General_Expense_Still_Never_Touches_Any_Product()
    {
        await using ExpensesDbContext db = TestDb.New();
        Expense expense = await SeedAsync(db, ExpenseSource.General, null, "Mağaza xərci", 40m);
        var products = new RecordingProductsModule();

        var result = await NewHandler(db, products).Handle(
            new UpdateExpenseCommand(
                expense.Id, "Mağaza icarəsi", "Mağaza xərci", General, 55m, null, null, null), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(products.Added);
        Assert.Empty(products.Removed);
        Assert.Empty(products.SnapshotsRead);
        Assert.Equal(55m, (await db.Expenses.SingleAsync()).Amount);
    }

    [Fact]
    public async Task Changing_The_Amount_Of_A_Product_Expense_Reverses_The_Old_And_Applies_The_New()
    {
        await using ExpensesDbContext db = TestDb.New();
        Guid productId = Guid.NewGuid();
        Expense expense = await SeedAsync(db, ExpenseSource.Product, productId, "Yol pulu", 30m);
        var products = new RecordingProductsModule(productId);

        var result = await NewHandler(db, products).Handle(
            new UpdateExpenseCommand(
                expense.Id, "Karqo", "Yol pulu", Product, 50m, null, productId, null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal((productId, "Yol pulu", 30m), Assert.Single(products.Removed));
        Assert.Equal((productId, "Yol pulu", 50m), Assert.Single(products.Added));
    }

    [Fact]
    public async Task General_Source_With_ProductId_Is_Rejected_On_The_Update_Path_Too()
    {
        await using ExpensesDbContext db = TestDb.New();
        Guid productId = Guid.NewGuid();
        Expense expense = await SeedAsync(db, ExpenseSource.Product, productId, "Yol pulu", 30m);
        var products = new RecordingProductsModule(productId);

        var result = await NewHandler(db, products).Handle(
            new UpdateExpenseCommand(
                expense.Id, "Karqo", "Yol pulu", General, 30m, null, productId, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Ümumi xərc üçün ProductId göndərilməməlidir", result.Error.Message);
        Assert.Empty(products.Removed); // rejected before the chain ran, so nothing was half-applied
        Assert.Empty(products.Added);
    }

    [Fact]
    public async Task Product_Source_Without_ProductId_Is_Rejected_On_The_Update_Path_Too()
    {
        await using ExpensesDbContext db = TestDb.New();
        Guid productId = Guid.NewGuid();
        Expense expense = await SeedAsync(db, ExpenseSource.Product, productId, "Yol pulu", 30m);
        var products = new RecordingProductsModule(productId);

        var result = await NewHandler(db, products).Handle(
            new UpdateExpenseCommand(expense.Id, "Karqo", "Yol pulu", Product, 30m, null, null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Mala bağlı xərc üçün ProductId tələb olunur", result.Error.Message);
        Assert.Empty(products.Removed);
        Assert.Empty(products.Added);
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

    private static UpdateExpenseHandler NewHandler(ExpensesDbContext db, RecordingProductsModule products) =>
        new(db,
            new FakeUnitOfWork(db),
            products,
            new FakeDayEndModule(),
            new UpdateExpenseValidator(),
            new FakeActivityLogger(),
            new FakeCurrentUser(Guid.NewGuid()),
            new FakeDateProvider());
}
