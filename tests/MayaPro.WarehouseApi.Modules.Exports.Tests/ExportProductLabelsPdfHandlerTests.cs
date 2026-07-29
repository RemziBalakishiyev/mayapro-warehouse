using System.Text;
using MayaPro.WarehouseApi.Modules.Exports.Application;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportProductLabelsPdf;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Exports.Tests;

/// <summary>
/// Unit tests for the label sheet: every rejected request shape (the endpoint's whole 400 surface) and the
/// produced document — magic bytes, file name and the copy count the sheet is allowed to carry.
/// </summary>
public sealed class ExportProductLabelsPdfHandlerTests
{
    private static readonly DateOnly Today = new(2026, 7, 30);
    private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondProductId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Rejects_A_Missing_Body()
    {
        Result<ExportFileResult> result = await Handler().Handle(null, default);

        Assert.True(result.IsFailure);
        Assert.Equal("Exports.NoLabelItems", result.Error.Code);
        Assert.Equal("Ən azı bir mal seçilməlidir", result.Error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task Rejects_An_Empty_Item_List(int? itemCount)
    {
        // itemCount null = "items": null, 0 = "items": []
        var request = new LabelsPdfRequest(
            itemCount is null ? null : Array.Empty<LabelItemRequest?>(), Type: null);

        Result<ExportFileResult> result = await Handler().Handle(request, default);

        Assert.True(result.IsFailure);
        Assert.Equal("Exports.NoLabelItems", result.Error.Code);
    }

    [Fact]
    public async Task Rejects_A_Null_Item_Instead_Of_Crashing()
    {
        var request = new LabelsPdfRequest([null], Type: null);

        Result<ExportFileResult> result = await Handler().Handle(request, default);

        Assert.True(result.IsFailure);
        Assert.Equal("Exports.NoLabelItems", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Rejects_A_Non_Positive_Count(int count)
    {
        Result<ExportFileResult> result = await Handler().Handle(Request((ProductId, count)), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Exports.InvalidLabelCount", result.Error.Code);
        Assert.Equal("Etiket sayı ən azı 1 olmalıdır", result.Error.Message);
    }

    [Fact]
    public async Task Rejects_More_Than_500_Labels_In_Total()
    {
        // Split over two entries: the cap is on the sheet, not on a single line.
        Result<ExportFileResult> result = await Handler()
            .Handle(Request((ProductId, 300), (SecondProductId, 201)), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Exports.TooManyLabels", result.Error.Code);
        Assert.Equal("Bir dəfəyə ən çoxu 500 etiket çap etmək olar", result.Error.Message);
    }

    [Fact]
    public async Task Accepts_Exactly_500_Labels()
    {
        Result<ExportFileResult> result = await Handler()
            .Handle(Request((ProductId, 250), (SecondProductId, 250)), default);

        Assert.True(result.IsSuccess);
        AssertIsPdf(result.Value);
    }

    /// <summary>
    /// A count large enough to overflow <c>int</c> when summed must still be the documented 400, not the
    /// 500 an <c>OverflowException</c> would produce.
    /// </summary>
    [Fact]
    public async Task Rejects_Counts_That_Would_Overflow_The_Total()
    {
        Result<ExportFileResult> result = await Handler()
            .Handle(Request((ProductId, int.MaxValue), (SecondProductId, int.MaxValue)), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Exports.TooManyLabels", result.Error.Code);
    }

    [Fact]
    public async Task Rejects_An_Unknown_Product()
    {
        Guid unknown = Guid.NewGuid();

        Result<ExportFileResult> result = await Handler().Handle(Request((unknown, 1)), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Exports.UnknownProducts", result.Error.Code);
        Assert.Contains(unknown.ToString(), result.Error.Message);
    }

    [Fact]
    public async Task Rejects_Products_Without_A_Barcode_And_Names_Them()
    {
        var handler = new ExportProductLabelsPdfHandler(
            new StubProductsModule(
                new ProductLabelInfo(ProductId, "Barkodsuz köynək", Barcode: "", 12.5m),
                new ProductLabelInfo(SecondProductId, "Barkodlu şalvar", "SDK0000002", 20m)),
            new FixedDateProvider(Today));

        Result<ExportFileResult> result = await handler
            .Handle(Request((ProductId, 1), (SecondProductId, 1)), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Exports.ProductsWithoutBarcode", result.Error.Code);
        Assert.Equal("Bu malların barkodu yoxdur: Barkodsuz köynək", result.Error.Message);
        Assert.DoesNotContain("Barkodlu şalvar", result.Error.Message);
    }

    /// <summary>Every barcode-less product is named once, however many copies were requested.</summary>
    [Fact]
    public async Task Names_Every_Barcodeless_Product_Once()
    {
        var handler = new ExportProductLabelsPdfHandler(
            new StubProductsModule(
                new ProductLabelInfo(ProductId, "Birinci", Barcode: "", 1m),
                new ProductLabelInfo(SecondProductId, "İkinci", Barcode: "   ", 2m)),
            new FixedDateProvider(Today));

        Result<ExportFileResult> result = await handler
            .Handle(Request((ProductId, 3), (SecondProductId, 2), (ProductId, 1)), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Bu malların barkodu yoxdur: Birinci, İkinci", result.Error.Message);
    }

    [Fact]
    public async Task Produces_A_Pdf_Named_After_The_Business_Date()
    {
        Result<ExportFileResult> result = await Handler().Handle(Request((ProductId, 10)), default);

        Assert.True(result.IsSuccess);
        AssertIsPdf(result.Value);
        Assert.Equal("etiketler-2026-07-30.pdf", result.Value.FileName);
        Assert.Equal("application/pdf", result.Value.ContentType);
    }

    [Fact]
    public async Task Produces_A_Pdf_For_Qr_Labels_Too()
    {
        var request = new LabelsPdfRequest([new LabelItemRequest(ProductId, 4)], Type: "QR");

        Result<ExportFileResult> result = await Handler().Handle(request, default);

        Assert.True(result.IsSuccess);
        AssertIsPdf(result.Value);
    }

    /// <summary>The same product may appear more than once; the ids are still looked up in one deduplicated call.</summary>
    [Fact]
    public async Task Accepts_A_Repeated_Product_And_Looks_It_Up_Once()
    {
        var productsModule = new StubProductsModule(
            new ProductLabelInfo(ProductId, "Təkrar mal", "SDK0000001", 5m));
        var handler = new ExportProductLabelsPdfHandler(productsModule, new FixedDateProvider(Today));

        Result<ExportFileResult> result = await handler
            .Handle(Request((ProductId, 2), (ProductId, 3)), default);

        Assert.True(result.IsSuccess);
        IReadOnlyCollection<Guid> lookedUp = Assert.Single(productsModule.LabelLookups);
        Assert.Equal(ProductId, Assert.Single(lookedUp));
    }

    /// <summary>
    /// 500 copies of one product must encode that barcode once and reference the same embedded image:
    /// a per-copy render would blow both the generation time and the file size up with the copy count.
    /// </summary>
    [Fact]
    public async Task Reuses_One_Code_Image_For_Every_Copy_Of_A_Barcode()
    {
        Result<ExportFileResult> result = await Handler().Handle(Request((ProductId, 500)), default);

        Assert.True(result.IsSuccess);
        // A single embedded PNG plus the font subset lands around 80 KB; one image per label would be
        // several times that.
        Assert.True(
            result.Value.Content.Length < 400_000,
            $"500-label sheet is {result.Value.Content.Length} bytes — the code image is not being reused");
    }

    private static void AssertIsPdf(ExportFileResult file)
    {
        Assert.NotEmpty(file.Content);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(file.Content, 0, 4));
    }

    private static ExportProductLabelsPdfHandler Handler() =>
        new(
            new StubProductsModule(
                new ProductLabelInfo(ProductId, "Uzun adlı test malı — kişi köynəyi, ağ, L", "SDK0000001", 12.5m),
                new ProductLabelInfo(SecondProductId, "İkinci mal", "SDK0000002", 7m)),
            new FixedDateProvider(Today));

    private static LabelsPdfRequest Request(params (Guid ProductId, int Count)[] items) =>
        new(items.Select(i => (LabelItemRequest?)new LabelItemRequest(i.ProductId, i.Count)).ToList(), Type: null);
}
