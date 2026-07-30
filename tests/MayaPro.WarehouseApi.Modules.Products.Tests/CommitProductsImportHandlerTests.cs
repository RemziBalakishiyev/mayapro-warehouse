using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Imports;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CommitProductsImport;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.PreviewProductsImport;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Tests;

/// <summary>
/// Unit tests for <see cref="CommitProductsImportHandler"/> — the happy path (new category + new product +
/// updated product, one activity entry), that error rows are always skipped, and the ways a token can fail
/// to commit (unknown, expired, already consumed, issued to another user). Mirrors BE#13's AC-9 to AC-13.
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
        var user = new FakeCurrentUser();

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: "Yeni mal", category: "Aksesuar", barcode: "NEW-1", purchasePrice: 15, salePrice: 25, quantity: 20),
            ImportWorkbookBuilder.Row(name: "Yenilənmiş mal", barcode: "EXIST-1", purchasePrice: 6, salePrice: 12, quantity: 40)
        ]);

        string token = await PreviewAsync(db, cache, user, file);

        var activityLogger = new FakeActivityLogger();
        CommitProductsImportHandler commit = NewCommitHandler(db, cache, user, activityLogger);

        Result commitResult = await commit.Handle(new CommitProductsImportCommand(token), default);

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
        var user = new FakeCurrentUser();

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: "Sağlam sətir", barcode: "OK-1"),
            ImportWorkbookBuilder.Row(name: "Xətalı sətir", salePrice: -5)
        ]);

        string token = await PreviewAsync(db, cache, user, file);

        Result commitResult = await NewCommitHandler(db, cache, user)
            .Handle(new CommitProductsImportCommand(token), default);

        Assert.True(commitResult.IsSuccess);
        Assert.Equal(1, await db.Products.CountAsync());
        Assert.Equal("Sağlam sətir", (await db.Products.SingleAsync()).Name);
    }

    [Fact]
    public async Task Update_Row_Whose_Product_Was_Deleted_Between_Preview_And_Commit_Is_Skipped()
    {
        await using ProductsDbContext db = NewDb();
        Product existing = CreateProduct("Silinəcək mal", barcode: "GONE-1", purchasePrice: 5, salePrice: 10, quantity: 3);
        db.Products.Add(existing);
        await db.SaveChangesAsync();

        var cache = new ImportTokenCache(new FakeDateProvider());
        var user = new FakeCurrentUser();

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: "Yenilənəcək idi", barcode: "GONE-1"),
            ImportWorkbookBuilder.Row(name: "Yeni mal", barcode: "STILL-NEW-1")
        ]);

        string token = await PreviewAsync(db, cache, user, file);

        db.Products.Remove(existing);
        await db.SaveChangesAsync();

        var activityLogger = new FakeActivityLogger();
        Result result = await NewCommitHandler(db, cache, user, activityLogger)
            .Handle(new CommitProductsImportCommand(token), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await db.Products.CountAsync());
        Assert.Equal("Excel import: 1 yeni, 0 yenilənmə", Assert.Single(activityLogger.Entries).Message);
    }

    [Fact]
    public async Task A_Barcode_Taken_Between_Preview_And_Commit_Aborts_The_Whole_Import()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var user = new FakeCurrentUser();

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: "Idxal malı", barcode: "RACE-1")
        ]);

        string token = await PreviewAsync(db, cache, user, file);

        // Someone adds that barcode by hand while the preview is on screen.
        db.Products.Add(CreateProduct("Əl ilə əlavə", barcode: "RACE-1", purchasePrice: 1, salePrice: 2, quantity: 1));
        await db.SaveChangesAsync();

        Result result = await NewCommitHandler(db, cache, user)
            .Handle(new CommitProductsImportCommand(token), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.BarcodeConflict, result.Error);
        Assert.Equal(1, await db.Products.CountAsync()); // only the hand-added one
    }

    [Fact]
    public async Task Unknown_Token_Returns_TokenNotFound_As_410()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());

        Result result = await NewCommitHandler(db, cache, new FakeCurrentUser())
            .Handle(new CommitProductsImportCommand("never-issued-token"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.TokenNotFound, result.Error);
    }

    [Fact]
    public async Task Expired_Token_Returns_TokenExpired()
    {
        await using ProductsDbContext db = NewDb();
        var clock = new FakeDateProvider();
        var cache = new ImportTokenCache(clock);
        var user = new FakeCurrentUser();

        byte[] file = ImportWorkbookBuilder.Build([ImportWorkbookBuilder.Row(barcode: "TTL-1")]);
        string token = await PreviewAsync(db, cache, user, file);

        // 10 dəqiqədən sonra — real vaxt gözləmədən, saatı irəli çəkirik.
        clock.UtcNow = clock.UtcNow.AddMinutes(11);

        Result result = await NewCommitHandler(db, cache, user)
            .Handle(new CommitProductsImportCommand(token), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.TokenExpired, result.Error);
        Assert.Equal(0, await db.Products.CountAsync());
    }

    [Fact]
    public async Task Committing_The_Same_Token_Twice_Fails_The_Second_Time()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var user = new FakeCurrentUser();

        byte[] file = ImportWorkbookBuilder.Build([ImportWorkbookBuilder.Row(barcode: "ONCE-1")]);
        string token = await PreviewAsync(db, cache, user, file);

        CommitProductsImportHandler commit = NewCommitHandler(db, cache, user);

        Result first = await commit.Handle(new CommitProductsImportCommand(token), default);
        Assert.True(first.IsSuccess);

        Result second = await commit.Handle(new CommitProductsImportCommand(token), default);
        Assert.True(second.IsFailure);
        Assert.Equal(ImportErrors.TokenNotFound, second.Error);
        Assert.Equal(1, await db.Products.CountAsync()); // not applied twice
    }

    [Fact]
    public async Task Another_Users_Token_Cannot_Be_Committed_And_Stays_Usable_By_Its_Owner()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var owner = new FakeCurrentUser();
        var stranger = new FakeCurrentUser();

        byte[] file = ImportWorkbookBuilder.Build([ImportWorkbookBuilder.Row(barcode: "MINE-1")]);
        string token = await PreviewAsync(db, cache, owner, file);

        Result stolen = await NewCommitHandler(db, cache, stranger)
            .Handle(new CommitProductsImportCommand(token), default);

        Assert.True(stolen.IsFailure);
        Assert.Equal(ImportErrors.TokenNotFound, stolen.Error);
        Assert.Equal(0, await db.Products.CountAsync());

        // The rejection must not consume the token: its owner can still commit.
        Result mine = await NewCommitHandler(db, cache, owner)
            .Handle(new CommitProductsImportCommand(token), default);

        Assert.True(mine.IsSuccess);
        Assert.Equal(1, await db.Products.CountAsync());
    }

    [Fact]
    public async Task Missing_Token_Returns_TokenNotFound()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());

        Result result = await NewCommitHandler(db, cache, new FakeCurrentUser())
            .Handle(new CommitProductsImportCommand(null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.TokenNotFound, result.Error);
    }

    [Fact]
    public async Task A_Failed_Transaction_Leaves_The_Token_Claimable_So_The_User_Can_Retry()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var user = new FakeCurrentUser();

        byte[] file = ImportWorkbookBuilder.Build([ImportWorkbookBuilder.Row(barcode: "RETRY-1")]);
        string token = await PreviewAsync(db, cache, user, file);

        var failing = new CommitProductsImportHandler(
            db, new ThrowingUnitOfWork(), cache, new FakeActivityLogger(), user);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => failing.Handle(new CommitProductsImportCommand(token), default));

        Result retry = await NewCommitHandler(db, cache, user)
            .Handle(new CommitProductsImportCommand(token), default);

        Assert.True(retry.IsSuccess);
        Assert.Equal(1, await db.Products.CountAsync());
    }

    private static async Task<string> PreviewAsync(
        ProductsDbContext db, IImportTokenCache cache, ICurrentUser user, byte[] file)
    {
        var preview = new PreviewProductsImportHandler(db, cache, user);
        Result<ImportPreviewResponse> result = await preview.Handle(new MemoryStream(file), file.Length, default);
        Assert.True(result.IsSuccess);
        return result.Value.ImportToken;
    }

    private static CommitProductsImportHandler NewCommitHandler(
        ProductsDbContext db,
        IImportTokenCache cache,
        ICurrentUser user,
        FakeActivityLogger? activityLogger = null) =>
        new(db, new FakeUnitOfWork(db), cache, activityLogger ?? new FakeActivityLogger(), user);

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

    /// <summary>Stands in for a database that refuses the transaction — the rollback path.</summary>
    private sealed class ThrowingUnitOfWork : IUnitOfWork
    {
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("transaction could not be started");
    }
}
