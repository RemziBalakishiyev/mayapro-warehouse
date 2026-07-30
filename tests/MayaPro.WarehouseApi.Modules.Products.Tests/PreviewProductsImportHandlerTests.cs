using MayaPro.WarehouseApi.Modules.Products.Application.Imports;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.PreviewProductsImport;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Tests;

/// <summary>
/// Unit tests for <see cref="PreviewProductsImportHandler"/> — row classification (create/update/error),
/// new-category detection, per-row validation, and the file-level 400s (empty, too many rows, too large,
/// wrong template). Mirrors BE#13's AC-2 through AC-7.
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

        PreviewProductsImportHandler handler = NewHandler(db);

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: "Yeni mal", barcode: "NEW-1"), // create
            ImportWorkbookBuilder.Row(name: "Yenilənən mal", barcode: "EXIST-1"), // update
            ImportWorkbookBuilder.Row(name: "Xətalı mal", salePrice: -10) // error: negative sale price
        ]);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

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
        PreviewProductsImportHandler handler = NewHandler(db);

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: "Aksesuar mal", category: "Aksesuar", barcode: "ACC-1")
        ]);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsSuccess);
        Assert.Contains("Aksesuar", result.Value.Summary.NewCategories);
        Assert.Equal(ImportRowStatus.Create, result.Value.Rows[0].Status);
    }

    [Fact]
    public async Task Empty_Name_Is_An_Error_Row()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        byte[] file = ImportWorkbookBuilder.Build([ImportWorkbookBuilder.Row(name: "", barcode: "NO-NAME-1")]);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsSuccess);
        Assert.Equal(ImportRowStatus.Error, result.Value.Rows[0].Status);
        Assert.Equal("Ad boşdur", result.Value.Rows[0].Error);
    }

    [Fact]
    public async Task NonNumeric_Purchase_Price_Is_An_Error_Row()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        byte[] file = ImportWorkbookBuilder.Build([ImportWorkbookBuilder.Row(purchasePrice: "abc")]);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsSuccess);
        Assert.Equal(ImportRowStatus.Error, result.Value.Rows[0].Status);
        Assert.Equal("Alış qiyməti rəqəm deyil", result.Value.Rows[0].Error);
    }

    [Fact]
    public async Task Negative_Sale_Price_Is_An_Error_Row()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        byte[] file = ImportWorkbookBuilder.Build([ImportWorkbookBuilder.Row(salePrice: -10)]);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsSuccess);
        Assert.Equal(ImportRowStatus.Error, result.Value.Rows[0].Status);
        Assert.Equal("Satış qiyməti mənfi", result.Value.Rows[0].Error);
    }

    [Theory]
    [InlineData("12,5")]  // decimal comma — the Azerbaijani/European keyboard
    [InlineData("12.5")]  // invariant decimal point
    [InlineData(" 12.5 ")] // stray spacing from a copy/paste
    public async Task Prices_Typed_As_Text_Are_Accepted_In_Either_Decimal_Notation(string typed)
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(barcode: "CULTURE-1", purchasePrice: typed, salePrice: typed)
        ]);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsSuccess);
        ImportRowResult row = result.Value.Rows[0];
        Assert.Equal(ImportRowStatus.Create, row.Status);
        Assert.Equal(12.5m, row.Data!.PurchasePrice);
        Assert.Equal(12.5m, row.Data.SalePrice);
    }

    [Fact]
    public async Task A_Barcode_Repeated_Inside_The_File_Is_An_Error_Row()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: "Birinci", barcode: "DUP-1"),
            ImportWorkbookBuilder.Row(name: "İkinci", barcode: "DUP-1")
        ]);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsSuccess);
        Assert.Equal(ImportRowStatus.Create, result.Value.Rows[0].Status);
        Assert.Equal(ImportRowStatus.Error, result.Value.Rows[1].Status);
        Assert.Equal("Barkod bu fayl daxilində təkrarlanır", result.Value.Rows[1].Error);
        Assert.Equal(1, result.Value.Summary.Creates);
        Assert.Equal(1, result.Value.Summary.Errors);
    }

    [Fact]
    public async Task An_Existing_Barcode_Repeated_Inside_The_File_Is_An_Error_Row_Not_Two_Updates()
    {
        await using ProductsDbContext db = NewDb();
        db.Products.Add(CreateProduct("Köhnə mal", barcode: "EXIST-DUP"));
        await db.SaveChangesAsync();

        PreviewProductsImportHandler handler = NewHandler(db);

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: "Birinci yenilənmə", barcode: "EXIST-DUP", salePrice: 30),
            ImportWorkbookBuilder.Row(name: "İkinci yenilənmə", barcode: "EXIST-DUP", salePrice: 40)
        ]);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsSuccess);
        Assert.Equal(ImportRowStatus.Update, result.Value.Rows[0].Status);
        Assert.Equal(ImportRowStatus.Error, result.Value.Rows[1].Status);
        Assert.Equal("Barkod bu fayl daxilində təkrarlanır", result.Value.Rows[1].Error);
        Assert.Equal(1, result.Value.Summary.Updates);
    }

    [Fact]
    public async Task A_Name_Longer_Than_The_Column_Is_An_Error_Row_Not_A_Failed_Commit()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: new string('A', 201), barcode: "LONG-1")
        ]);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsSuccess);
        Assert.Equal(ImportRowStatus.Error, result.Value.Rows[0].Status);
        Assert.Equal("Ad 200 simvoldan uzun ola bilməz", result.Value.Rows[0].Error);
    }

    [Fact]
    public async Task More_Attributes_Than_A_Product_Allows_Is_An_Error_Row()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        string attributes = string.Join("; ", Enumerable.Range(1, 16).Select(i => $"Xüsusiyyət{i}: {i}"));
        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(barcode: "ATTR-1", attributes: attributes)
        ]);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsSuccess);
        Assert.Equal(ImportRowStatus.Error, result.Value.Rows[0].Status);
        Assert.Equal("Ən çoxu 15 xüsusiyyət əlavə etmək olar", result.Value.Rows[0].Error);
    }

    [Fact]
    public async Task Attributes_Are_Parsed_Into_Name_Value_Pairs()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(barcode: "ATTR-2", attributes: "Ölçü: M; Rəng: Qara")
        ]);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsSuccess);
        var attributes = result.Value.Rows[0].Data!.Attributes;
        Assert.Collection(
            attributes,
            a => Assert.Equal(("Ölçü", "M"), (a.Name, a.Value)),
            a => Assert.Equal(("Rəng", "Qara"), (a.Name, a.Value)));
    }

    [Fact]
    public async Task A_Row_That_Is_Blank_In_Every_Column_Is_Skipped_Not_Reported_As_An_Error()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        byte[] file = ImportWorkbookBuilder.Build([
            ImportWorkbookBuilder.Row(name: "Real mal", barcode: "BLANK-1"),
            ImportWorkbookBuilder.BlankRow()
        ]);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Rows);
        Assert.Equal(ImportRowStatus.Create, result.Value.Rows[0].Status);
        Assert.Equal(0, result.Value.Summary.Errors);
    }

    [Fact]
    public async Task Null_File_Returns_EmptyFile_Error()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        Result<ImportPreviewResponse> result = await handler.Handle(null, 0, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.EmptyFile, result.Error);
    }

    [Fact]
    public async Task Header_Only_File_Returns_EmptyFile_Error()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        byte[] file = ImportWorkbookBuilder.BuildHeaderOnly();

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.EmptyFile, result.Error);
    }

    [Fact]
    public async Task More_Than_1000_Data_Rows_Returns_TooManyRows_Error()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        var rows = Enumerable.Range(0, 1001)
            .Select(i => ImportWorkbookBuilder.Row(name: $"Mal {i}"))
            .ToList();
        byte[] file = ImportWorkbookBuilder.Build(rows);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.TooManyRows, result.Error);
    }

    [Fact]
    public async Task Exactly_1000_Data_Rows_Is_Still_Accepted()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        var rows = Enumerable.Range(0, 1000)
            .Select(i => ImportWorkbookBuilder.Row(name: $"Mal {i}", barcode: $"BULK-{i}"))
            .ToList();
        byte[] file = ImportWorkbookBuilder.Build(rows);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsSuccess);
        Assert.Equal(1000, result.Value.Summary.Creates);
    }

    [Fact]
    public async Task An_Upload_Over_The_Size_Limit_Is_Rejected_Before_It_Is_Read()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        // Length alone decides: the stream is never touched, which is the point of the guard.
        Result<ImportPreviewResponse> result = await handler.Handle(
            new MemoryStream([1, 2, 3]), 6 * 1024 * 1024, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.FileTooLarge, result.Error);
    }

    [Fact]
    public async Task Mismatched_Headers_Return_InvalidTemplate_Error()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        string[] wrongHeaders = ["Ad", "Qiymət", "Say"]; // not the template's shape
        byte[] file = ImportWorkbookBuilder.Build([ImportWorkbookBuilder.Row()], wrongHeaders);

        Result<ImportPreviewResponse> result = await Preview(handler, file);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.InvalidTemplate, result.Error);
        Assert.Equal("Şablona uyğun deyil — şablonu endirib istifadə et", result.Error.Message);
    }

    [Fact]
    public async Task Not_An_Excel_File_Returns_InvalidTemplate_Error()
    {
        await using ProductsDbContext db = NewDb();
        PreviewProductsImportHandler handler = NewHandler(db);

        byte[] garbage = "this is not an xlsx file"u8.ToArray();

        Result<ImportPreviewResponse> result = await Preview(handler, garbage);

        Assert.True(result.IsFailure);
        Assert.Equal(ImportErrors.InvalidTemplate, result.Error);
    }

    private static Task<Result<ImportPreviewResponse>> Preview(
        PreviewProductsImportHandler handler, byte[] file) =>
        handler.Handle(new MemoryStream(file), file.Length, default);

    private static PreviewProductsImportHandler NewHandler(ProductsDbContext db) =>
        new(db, new ImportTokenCache(new FakeDateProvider()), new FakeCurrentUser());

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
