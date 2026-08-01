using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.GenerateBarcode;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Tests;

/// <summary>
/// Unit tests for <see cref="GenerateBarcodeHandler"/>: the happy path (a barcode-less product gets a
/// unique <c>"SDK" + 7 digits</c> code), the already-has-a-barcode rejection, and the unknown-id case.
/// </summary>
public sealed class GenerateBarcodeHandlerTests
{
    [Fact]
    public async Task Assigns_Unique_SDK_Barcode_To_Product_Without_One()
    {
        await using ProductsDbContext db = NewDb();
        Product product = CreateProduct(barcode: "");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var handler = new GenerateBarcodeHandler(db);
        var result = await handler.Handle(product.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Matches("^SDK[0-9]{7}$", result.Value.Barcode);

        Product persisted = await db.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(result.Value.Barcode, persisted.Barcode);
    }

    [Fact]
    public async Task Does_Not_Reuse_A_Barcode_Already_Taken_By_Another_Product()
    {
        await using ProductsDbContext db = NewDb();
        Product taken = CreateProduct(barcode: "SDK1234567");
        Product target = CreateProduct(barcode: "");
        db.Products.AddRange(taken, target);
        await db.SaveChangesAsync();

        var handler = new GenerateBarcodeHandler(db);
        var result = await handler.Handle(target.Id, default);

        Assert.True(result.IsSuccess);
        Assert.NotEqual("SDK1234567", result.Value.Barcode);
    }

    /// <summary>Generating in bulk must never hand out the same code twice.</summary>
    [Fact]
    public async Task Gives_Every_Product_A_Distinct_Barcode()
    {
        const int productCount = 50;
        await using ProductsDbContext db = NewDb();
        List<Product> products = Enumerable.Range(0, productCount)
            .Select(_ => CreateProduct(barcode: ""))
            .ToList();
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        var handler = new GenerateBarcodeHandler(db);
        var barcodes = new List<string>();
        foreach (Product product in products)
        {
            var result = await handler.Handle(product.Id, default);
            Assert.True(result.IsSuccess);
            barcodes.Add(result.Value.Barcode);
        }

        Assert.All(barcodes, barcode => Assert.Matches("^SDK[0-9]{7}$", barcode));
        Assert.Equal(productCount, barcodes.Distinct().Count());
    }

    /// <summary>
    /// The read that looks for a free code cannot see a concurrent writer, so the unique index is what
    /// actually enforces uniqueness. When it rejects a save, the handler must take a fresh code and try
    /// again instead of surfacing the database error.
    /// </summary>
    [Fact]
    public async Task Retries_With_A_New_Code_When_The_Unique_Index_Rejects_The_Save()
    {
        await using ProductsDbContext db = NewDb();
        Product product = CreateProduct(barcode: "");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var racing = new SaveFailsFirstAttemptsDbContext(db, failures: 2);
        var handler = new GenerateBarcodeHandler(racing);

        var result = await handler.Handle(product.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Matches("^SDK[0-9]{7}$", result.Value.Barcode);
        Assert.Equal(3, racing.SaveAttempts); // two rejected races, then the winning save
    }

    /// <summary>A database failure that survives every retry is a real fault and must not be swallowed.</summary>
    [Fact]
    public async Task Surfaces_The_Database_Error_When_Every_Attempt_Is_Rejected()
    {
        await using ProductsDbContext db = NewDb();
        Product product = CreateProduct(barcode: "");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var alwaysFailing = new SaveFailsFirstAttemptsDbContext(db, failures: int.MaxValue);
        var handler = new GenerateBarcodeHandler(alwaysFailing);

        await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(product.Id, default));
        Assert.Equal(5, alwaysFailing.SaveAttempts); // bounded — no infinite retry loop
    }

    [Fact]
    public async Task Returns_BarcodeAlreadyExists_When_Product_Already_Has_A_Barcode()
    {
        await using ProductsDbContext db = NewDb();
        Product product = CreateProduct(barcode: "SDK0000001");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var handler = new GenerateBarcodeHandler(db);
        var result = await handler.Handle(product.Id, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ProductErrors.BarcodeAlreadyExists, result.Error);
        Assert.Equal("Malın artıq barkodu var", result.Error.Message);

        Product persisted = await db.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal("SDK0000001", persisted.Barcode); // unchanged
    }

    [Fact]
    public async Task Returns_NotFound_For_Unknown_Product()
    {
        await using ProductsDbContext db = NewDb();
        var handler = new GenerateBarcodeHandler(db);

        var result = await handler.Handle(Guid.NewGuid(), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ProductErrors.NotFound, result.Error);
    }

    private static Product CreateProduct(string barcode) =>
        Product.Create(
            name: "Test məhsul",
            category: "Test",
            attributes: new List<ProductAttribute>(),
            barcode: barcode,
            image: string.Empty,
            note: string.Empty,
            purchasePrice: 10,
            salePrice: 20,
            quantity: 5,
            minStock: 1,
            currency: "AZN",
            supplierId: "sup_1",
            location: "Anbar A / Rəf 1 / Qutu 1",
            store: "Anbar A",
            warehouse: "Anbar A",
            shelf: "1",
            box: "1",
            expenses: ProductExpenses.Empty);

    private static ProductsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase($"products-tests-{Guid.NewGuid()}")
            .Options;
        return new ProductsDbContext(options);
    }

    /// <summary>
    /// Stands in for the race the in-memory provider cannot reproduce: the first
    /// <paramref name="failures"/> saves are rejected the way SQL Server rejects a duplicate key, the
    /// rest go through to the real context.
    /// </summary>
    private sealed class SaveFailsFirstAttemptsDbContext(ProductsDbContext inner, int failures)
        : IProductsDbContext
    {
        public int SaveAttempts { get; private set; }

        public DbSet<Product> Products => inner.Products;

        public DbSet<Category> Categories => inner.Categories;

        public DbSet<StockAdjustment> StockAdjustments => inner.StockAdjustments;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveAttempts++;
            if (SaveAttempts <= failures)
                throw new DbUpdateException(
                    "Cannot insert duplicate key row in object 'products.Products' with unique index 'IX_Products_Barcode'.");

            return inner.SaveChangesAsync(cancellationToken);
        }
    }
}
