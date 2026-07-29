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
}
