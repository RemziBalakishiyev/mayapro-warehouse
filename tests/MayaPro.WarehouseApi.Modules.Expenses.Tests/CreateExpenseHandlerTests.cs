using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpense;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>
/// Unit tests for <see cref="CreateExpenseHandler"/>, centred on the core business rule (AC-4 / TC-4): a
/// <c>general</c> expense must never reach the product real-cost (maya) chain. The
/// <see cref="RecordingProductsModule"/> stands in for the Products module and records every call, so
/// "maya was not touched" is asserted directly instead of inferred from a cost figure. AC-5 / TC-5 covers
/// the opposite direction: a <c>product</c> expense still runs the old chain exactly once.
/// </summary>
public sealed class CreateExpenseHandlerTests
{
    private const string General = WireFormat.ExpenseSources.General;
    private const string Product = WireFormat.ExpenseSources.Product;

    [Fact]
    public async Task General_Expense_Never_Calls_The_Product_Cost_Chain()
    {
        await using ExpensesDbContext db = TestDb.New();
        var products = new RecordingProductsModule();
        CreateExpenseHandler handler = NewHandler(db, products);

        var result = await handler.Handle(
            new CreateExpenseCommand("Mağaza icarəsi", "Mağaza xərci", General, 600m, null, null, null), default);

        Assert.True(result.IsSuccess);
        // The whole point of AC-4: no product was read, raised or lowered.
        Assert.Empty(products.Added);
        Assert.Empty(products.Removed);
        Assert.Empty(products.SnapshotsRead);

        Expense stored = await db.Expenses.SingleAsync();
        Assert.Equal(ExpenseSource.General, stored.Source);
        Assert.Null(stored.ProductId);
        Assert.Null(stored.ProductName);
        Assert.Equal("Mağaza xərci", stored.Category);
        Assert.Equal(General, result.Value.Source);
    }

    [Fact]
    public async Task Product_Expense_Raises_That_Products_Cost_Exactly_Once()
    {
        await using ExpensesDbContext db = TestDb.New();
        Guid productId = Guid.NewGuid();
        var products = new RecordingProductsModule(productId);
        CreateExpenseHandler handler = NewHandler(db, products);

        var result = await handler.Handle(
            new CreateExpenseCommand("Karqo", "Yol pulu", Product, 30m, null, productId, null), default);

        Assert.True(result.IsSuccess);
        (Guid ProductId, string Category, decimal Amount) call = Assert.Single(products.Added);
        Assert.Equal(productId, call.ProductId);
        // The expense type name becomes the product's free-form cost line name.
        Assert.Equal("Yol pulu", call.Category);
        Assert.Equal(30m, call.Amount);

        Expense stored = await db.Expenses.SingleAsync();
        Assert.Equal(ExpenseSource.Product, stored.Source);
        Assert.Equal(productId, stored.ProductId);
        Assert.Equal("Test malı", stored.ProductName);
        Assert.Equal(Product, result.Value.Source);
    }

    [Fact]
    public async Task Product_Source_Without_ProductId_Is_Rejected_And_Touches_Nothing()
    {
        await using ExpensesDbContext db = TestDb.New();
        var products = new RecordingProductsModule();
        CreateExpenseHandler handler = NewHandler(db, products);

        var result = await handler.Handle(
            new CreateExpenseCommand("Karqo", "Yol pulu", Product, 30m, null, null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Mala bağlı xərc üçün ProductId tələb olunur", result.Error.Message);
        Assert.Empty(products.Added);
        Assert.Equal(0, await db.Expenses.CountAsync());
    }

    [Fact]
    public async Task General_Source_With_ProductId_Is_Rejected_And_Touches_Nothing()
    {
        await using ExpensesDbContext db = TestDb.New();
        Guid productId = Guid.NewGuid();
        var products = new RecordingProductsModule(productId);
        CreateExpenseHandler handler = NewHandler(db, products);

        var result = await handler.Handle(
            new CreateExpenseCommand("Mağaza icarəsi", "Mağaza xərci", General, 600m, null, productId, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Ümumi xərc üçün ProductId göndərilməməlidir", result.Error.Message);
        Assert.Empty(products.Added);
        Assert.Equal(0, await db.Expenses.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Product")]   // wire codes are lower-case; a near-miss must not be coerced
    [InlineData("umumi")]
    public async Task Unknown_Source_Is_Rejected_With_A_Validation_Error(string? source)
    {
        await using ExpensesDbContext db = TestDb.New();
        var products = new RecordingProductsModule();
        CreateExpenseHandler handler = NewHandler(db, products);

        var result = await handler.Handle(
            new CreateExpenseCommand("Xərc", "Yol pulu", source!, 30m, null, null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Xərc mənbəyi yanlışdır", result.Error.Message);
        Assert.Empty(products.Added);
        Assert.Equal(0, await db.Expenses.CountAsync());
    }

    [Fact]
    public async Task Category_Longer_Than_The_Column_Is_Rejected_As_Validation_Not_A_Database_Error()
    {
        await using ExpensesDbContext db = TestDb.New();
        var products = new RecordingProductsModule();
        CreateExpenseHandler handler = NewHandler(db, products);

        var result = await handler.Handle(
            new CreateExpenseCommand("Xərc", new string('x', 101), General, 30m, null, null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Xərc növü 100 simvoldan uzun ola bilməz", result.Error.Message);
        Assert.Equal(0, await db.Expenses.CountAsync());
    }

    [Fact]
    public async Task Missing_Product_Rolls_The_Whole_Thing_Back()
    {
        await using ExpensesDbContext db = TestDb.New();
        var products = new RecordingProductsModule(); // knows no products
        CreateExpenseHandler handler = NewHandler(db, products);

        var result = await handler.Handle(
            new CreateExpenseCommand("Karqo", "Yol pulu", Product, 30m, null, Guid.NewGuid(), null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Products.NotFound", result.Error.Code);
        Assert.Empty(products.Added);            // never got past the snapshot read
        Assert.Equal(0, await db.Expenses.CountAsync());
    }

    private static CreateExpenseHandler NewHandler(ExpensesDbContext db, RecordingProductsModule products) =>
        new(db,
            new FakeUnitOfWork(db),
            products,
            new CreateExpenseValidator(),
            new FakeActivityLogger(),
            new FakeCurrentUser(Guid.NewGuid()));
}
