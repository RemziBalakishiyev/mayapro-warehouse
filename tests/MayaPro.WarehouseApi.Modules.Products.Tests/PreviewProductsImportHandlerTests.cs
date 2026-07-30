using MayaPro.WarehouseApi.Modules.Products.Application.Imports;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.PreviewProductsImport;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Tests;

/// <summary>
/// Unit tests for <see cref="PreviewProductsImportHandler"/> — row classification (create/update/error),
/// new-category detection, and the file-level 400s (empty, too many rows, wrong template). Mirrors BE#13's
/// AC-2 through AC-7.
/// </summary>
public sealed class PreviewProductsImportHandlerTests
{
    [Fact]
    public async Task Classifies_Create_Update_And_Error_Rows_Without_Writing_To_The_Database()
    {
        await using ProductsDbContext db = NewDb();
        Product existing = CreateProduct("Köhnə mal", barcode: "EXIST-1");
        db.Products.Add(existing);
        await db.SaveChangesAsync();

        var cache = new ImportTokenCache(new FakeDateProvider());
        var handler = new PreviewProductsImportHandler(db, cache);

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: "Yeni mal", barcode: "NEW-1"), // create
            ImportWorkbookBuilder.Row(name: "Yenilənən mal", barcode: "EXIST-1"), // update
            ImportWorkbookBuilder.Row(name: "Xətalı mal", salePrice: -10) // error: negative sale price
        ]);

        var result = await handler.Handle(new FakeFormFile(file), default);

        Assert.True(result.IsSuccess);
        ImportPreviewResponse response = result.Value;
        Assert.Equal(1, response.Summary.Creates);
        Assert.Equal(1, response.Summary.Updates);
        Assert.Equal(1, response.Summary.Errors);
        Assert.Equal(3, response.Rows.Count);

        Assert.Equal(ImportRowStatus.Create, response.Rows[0].Status);
        Assert.Equal(ImportRowStatus.Update, response.Rows[1].Status);
        Assert.Equal(ImportRowStatus.Error, response.Rows[2].Status);
        Assert.Equal("Satış qiyməti mənfi", response.Rows[2].Error);

        // DB-yə heç nə yazılmayıb.
        Assert.Equal(1, await db.Products.CountAsync());
        Assert.Equal(0, await db.Categories.CountAsync());
    }

    [Fact]
    public async Task Flags_A_Category_That_Does_Not_Exist_Yet_As_New_On_A_Create_Row()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var handler = new PreviewProductsImportHandler(db, cache);

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: "Aksesuar mal", category: "Aksesuar", barcode: "ACC-1")
        ]);

        var result = await handler.Handle(new FakeFormFile(file), default);

        Assert.True(result.IsSuccess);
        Assert.Contains("Aksesuar", result.Value.Summary.NewCategories);
        Assert.Equal(ImportRowStatus.Create, result.Value.Rows[0].Status);
    }

    [Fact]
    public async Task Empty_Name_Is_An_Error_Row()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var handler = new PreviewProductsImportHandler(db, cache);

        byte[] file = ImportWorkbookBuilder.Build([ImportWorkbookBuilder.Row(name: "")]);

        var result = await handler.Handle(new FakeFormFile(file), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(ImportRowStatus.Error, result.Value.Rows[0].Status);
        Assert.Equal("Ad boşdur", result.Value.Rows[0].Error);
    }

    [Fact]
    public async Task NonNumeric_Purchase_Price_Is_An_Error_Row()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var handler = new PreviewProductsImportHandler(db, cache);

        byte[] file = ImportWorkbookBuilder.Build([ImportWorkbookBuilder.Row(purchasePrice: "abc")]);

        var result = await handler.Handle(new FakeFormFile(file), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(ImportRowStatus.Error, result.Value.Rows[0].Status);
        Assert.Equal("Alış qiyməti rəqəm deyil", result.Value.Rows[0].Error);
    }

    [Fact]
    public async Task Negative_Sale_Price_Is_An_Error_Row()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var handler = new PreviewProductsImportHandler(db, cache);

        byte[] file = ImportWorkbookBuilder.Build([ImportWorkbookBuilder.Row(salePrice: -10)]);

        var result = await handler.Handle(new FakeFormFile(file), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(ImportRowStatus.Error, result.Value.Rows[0].Status);
        Assert.Equal("Satış qiyməti mənfi", result.Value.Rows[0].Error);
    }

    [Fact]
    public async Task Null_File_Returns_EmptyFile_Error()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var handler = new PreviewProductsImportHandler(db, cache);

        var result = await handler.Handle(null, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.EmptyFile, result.Error);
    }

    [Fact]
    public async Task Header_Only_File_Returns_EmptyFile_Error()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var handler = new PreviewProductsImportHandler(db, cache);

        byte[] file = ImportWorkbookBuilder.BuildHeaderOnly();

        var result = await handler.Handle(new FakeFormFile(file), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.EmptyFile, result.Error);
    }

    [Fact]
    public async Task More_Than_1000_Data_Rows_Returns_TooManyRows_Error()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var handler = new PreviewProductsImportHandler(db, cache);

        var rows = Enumerable.Range(0, 1001)
            .Select(i => ImportWorkbookBuilder.Row(name: $"Mal {i}"))
            .ToList();
        byte[] file = ImportWorkbookBuilder.Build(rows);

        var result = await handler.Handle(new FakeFormFile(file), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.TooManyRows, result.Error);
    }

    [Fact]
    public async Task Mismatched_Headers_Return_InvalidTemplate_Error()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var handler = new PreviewProductsImportHandler(db, cache);

        string[] wrongHeaders = ["Ad", "Qiymət", "Say"]; // not the template's shape
        byte[] file = ImportWorkbookBuilder.Build(
            [ImportWorkbookBuilder.Row()],
            wrongHeaders);

        var result = await handler.Handle(new FakeFormFile(file), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.InvalidTemplate, result.Error);
        Assert.Equal("Şablona uyğun deyil — şablonu endirib istifadə et", result.Error.Message);
    }

    [Fact]
    public async Task Not_An_Excel_File_Returns_InvalidTemplate_Error()
    {
        await using ProductsDbContext db = NewDb();
        var cache = new ImportTokenCache(new FakeDateProvider());
        var handler = new PreviewProductsImportHandler(db, cache);

        byte[] garbage = "this is not an xlsx file"u8.ToArray();

        var result = await handler.Handle(new FakeFormFile(garbage), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.InvalidTemplate, result.Error);
    }

    private static Product CreateProduct(string name, string barcode) =>
        Product.Create(
            name,
            category: "Test",
            attributes: new List<ProductAttribute>(),
            barcode,
            image: string.Empty,
            note: string.Empty,
            purchasePrice: 5,
            salePrice: 10,
            quantity: 3,
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
            .UseInMemoryDatabase($"products-import-tests-{Guid.NewGuid()}")
            .Options;
        return new ProductsDbContext(options);
    }
}
