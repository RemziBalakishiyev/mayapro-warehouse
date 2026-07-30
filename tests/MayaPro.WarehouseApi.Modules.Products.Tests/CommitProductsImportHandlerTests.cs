using MayaPro.WarehouseApi.Modules.Products.Application.Imports;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CommitProductsImport;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.PreviewProductsImport;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Tests;

/// <summary>
/// Unit tests for <see cref="CommitProductsImportHandler"/> — the happy path (new category + new product +
/// updated product, one activity entry), that error rows are always skipped, and the three ways a token can
/// fail to commit (unknown, expired, already consumed). Mirrors BE#13's AC-9 through AC-13.
/// </summary>
public sealed class CommitProductsImportHandlerTests
{
    [Fact]
    public async Task Commits_New_Category_New_Product_And_Updated_Product_In_One_Transaction_With_Activity_Log()
    {
        await using ProductsDbContext db = NewDb();
        Product existing = CreateProduct("Köhnə mal", barcode: "EXIST-1", purchasePrice: 5, salePrice: 10, quantity: 3);
        db.Products.Add(existing);
        await db.SaveChangesAsync();

        var cache = new ImportTokenCache(new FakeDateProvider());
        var preview = new PreviewProductsImportHandler(db, cache);

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: "Yeni mal", category: "Aksesuar", barcode: "NEW-1", purchasePrice: 15, salePrice: 25, quantity: 20),
            ImportWorkbookBuilder.Row(name: "Yenilənmiş mal", barcode: "EXIST-1", purchasePrice: 6, salePrice: 12, quantity: 40)
        ]);

        var previewResult = await preview.Handle(new FakeFormFile(file), default);
        Assert.True(previewResult.IsSuccess);
        string token = previewResult.Value.ImportToken;

        var activityLogger = new FakeActivityLogger();
        var commit = new CommitProductsImportHandler(
            db, new FakeUnitOfWork(db), cache, activityLogger, new FakeCurrentUser());

        var commitResult = await commit.Handle(new CommitProductsImportCommand(token), default);

        Assert.True(commitResult.IsSuccess);

        Assert.Equal(1, await db.Categories.CountAsync(c => c.Name == "Aksesuar"));

        Product created = await db.Products.SingleAsync(p => p.Barcode == "NEW-1");
        Assert.Equal("Yeni mal", created.Name);
        Assert.Equal(20, created.Quantity);
        Assert.Equal(20, created.InitialQuantity);
        Assert.Equal(15, created.RealCostPerUnit); // RealCost = purchase price, no expenses

        Product updated = await db.Products.SingleAsync(p => p.Barcode == "EXIST-1");
        Assert.Equal("Yenilənmiş mal", updated.Name);
        Assert.Equal(6, updated.PurchasePrice);
        Assert.Equal(12, updated.SalePrice);
        Assert.Equal(40, updated.Quantity);
        Assert.Equal(3, updated.InitialQuantity); // fixed at the product's own creation, never touched by import

        (string Type, string Message) entry = Assert.Single(activityLogger.Entries);
        Assert.Equal("Excel idxalı", entry.Type);
        Assert.Equal("Excel import: 1 yeni, 1 yenilənmə", entry.Message);
    }

    [Fact]
    public async Task Commit_Skips_Error_Rows_And_Only_Applies_Valid_Ones()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var preview = new PreviewProductsImportHandler(db, cache);

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: "Sağlam sətir", barcode: "OK-1"),
            ImportWorkbookBuilder.Row(name: "Xətalı sətir", salePrice: -5)
        ]);

        var previewResult = await preview.Handle(new FakeFormFile(file), default);
        Assert.True(previewResult.IsSuccess);
        Assert.Equal(1, previewResult.Value.Summary.Creates);
        Assert.Equal(1, previewResult.Value.Summary.Errors);

        var commit = new CommitProductsImportHandler(
            db, new FakeUnitOfWork(db), cache, new FakeActivityLogger(), new FakeCurrentUser());

        var commitResult = await commit.Handle(new CommitProductsImportCommand(previewResult.Value.ImportToken), default);

        Assert.True(commitResult.IsSuccess);
        Assert.Equal(1, await db.Products.CountAsync());
        Assert.Equal("Sağlam sətir", (await db.Products.SingleAsync()).Name);
    }

    [Fact]
    public async Task Unknown_Token_Returns_TokenNotFound_As_410()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var commit = new CommitProductsImportHandler(
            db, new FakeUnitOfWork(db), cache, new FakeActivityLogger(), new FakeCurrentUser());

        var result = await commit.Handle(new CommitProductsImportCommand("never-issued-token"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.TokenNotFound, result.Error);
    }

    [Fact]
    public async Task Expired_Token_Returns_TokenExpired()
    {
        await using ProductsDbContext db = NewDb();
        var clock = new FakeDateProvider();
        var cache = new ImportTokenCache(clock);
        var preview = new PreviewProductsImportHandler(db, cache);

        byte[] file = ImportWorkbookBuilder.Build([ImportWorkbookBuilder.Row(barcode: "TTL-1")]);
        var previewResult = await preview.Handle(new FakeFormFile(file), default);
        Assert.True(previewResult.IsSuccess);

        // 10 dəqiqədən sonra — real vaxt gözləmədən, saatı irəli çəkirik.
        clock.UtcNow = clock.UtcNow.AddMinutes(11);

        var commit = new CommitProductsImportHandler(
            db, new FakeUnitOfWork(db), cache, new FakeActivityLogger(), new FakeCurrentUser());
        var commitResult = await commit.Handle(new CommitProductsImportCommand(previewResult.Value.ImportToken), default);

        Assert.True(commitResult.IsFailure);
        Assert.Equal(ImportErrors.TokenExpired, commitResult.Error);
        Assert.Equal(0, await db.Products.CountAsync());
    }

    [Fact]
    public async Task Committing_The_Same_Token_Twice_Fails_The_Second_Time()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var preview = new PreviewProductsImportHandler(db, cache);

        byte[] file = ImportWorkbookBuilder.Build([ImportWorkbookBuilder.Row(barcode: "ONCE-1")]);
        var previewResult = await preview.Handle(new FakeFormFile(file), default);
        string token = previewResult.Value.ImportToken;

        var commit = new CommitProductsImportHandler(
            db, new FakeUnitOfWork(db), cache, new FakeActivityLogger(), new FakeCurrentUser());

        var first = await commit.Handle(new CommitProductsImportCommand(token), default);
        Assert.True(first.IsSuccess);

        var second = await commit.Handle(new CommitProductsImportCommand(token), default);
        Assert.True(second.IsFailure);
        Assert.Equal(1, await db.Products.CountAsync()); // not applied twice
    }

    [Fact]
    public async Task Missing_Token_Returns_TokenNotFound()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var commit = new CommitProductsImportHandler(
            db, new FakeUnitOfWork(db), cache, new FakeActivityLogger(), new FakeCurrentUser());

        var result = await commit.Handle(new CommitProductsImportCommand(null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.TokenNotFound, result.Error);
    }

    private static Product CreateProduct(
        string name, string barcode, decimal purchasePrice, decimal salePrice, int quantity) =>
        Product.Create(
            name,
            category: "Test",
            attributes: new List<ProductAttribute>(),
            barcode,
            image: string.Empty,
            note: string.Empty,
            purchasePrice,
            salePrice,
            quantity,
            minStock: 1,
            currency: "AZN",
            supplierId: "sup_1",
            location: "Anbar A",
            store: "Anbar A",
            warehouse: "Anbar A",
            shelf: "1",
            box: "1",
            expenses: ProductExpenses.Empty);

    private static ProductsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase($"products-import-commit-tests-{Guid.NewGuid()}")
            .Options;
        return new ProductsDbContext(options);
    }
}
