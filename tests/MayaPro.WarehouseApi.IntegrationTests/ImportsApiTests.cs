using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for the two-step Excel import flow: <c>POST /api/imports/products/preview</c> (parses,
/// classifies, never writes) and <c>POST /api/imports/products/commit</c> (applies exactly the previewed
/// result once). Mirrors BE#13's test cases TC-2 through TC-17 at the HTTP level; row-classification detail
/// and the token cache's own behaviour are covered by the Products module's unit tests.
/// <para>
/// The uploaded files are built from the shared <see cref="ProductImportTemplate"/> header contract — the
/// same constant the downloadable template is written from — so these tests upload exactly what a real user
/// would.
/// </para>
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ImportsApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public ImportsApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Preview_Classifies_A_Mixed_File_And_Writes_Nothing_To_The_Database()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var existing = await client.CreateProductAsync("IMP-EXIST-1", quantity: 3, salePrice: 10m, purchasePrice: 5m);
        int productsBefore = (await client.GetFromJsonAsync<List<IntegrationTestHelpers.ProductDto>>("/api/products"))!.Count;

        byte[] file = BuildWorkbook([
            Row(name: "Yeni idxal malı", barcode: "IMP-NEW-1"),
            Row(name: "Yenilənəcək mal", barcode: "IMP-EXIST-1"),
            Row(name: "Xətalı mal", salePrice: -10)
        ]);

        HttpResponseMessage response = await client.PostAsync(
            "/api/imports/products/preview", MultipartFile(file));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PreviewResponseDto>())!;

        Assert.False(string.IsNullOrWhiteSpace(body.ImportToken));
        Assert.Equal(1, body.Summary.Creates);
        Assert.Equal(1, body.Summary.Updates);
        Assert.Equal(1, body.Summary.Errors);
        Assert.Equal("create", body.Rows[0].Status);
        Assert.Equal("update", body.Rows[1].Status);
        Assert.Equal("error", body.Rows[2].Status);
        Assert.Equal("Satış qiyməti mənfi", body.Rows[2].Error);

        int productsAfter = (await client.GetFromJsonAsync<List<IntegrationTestHelpers.ProductDto>>("/api/products"))!.Count;
        Assert.Equal(productsBefore, productsAfter); // preview must not touch the database
        _ = existing;
    }

    [Fact]
    public async Task Preview_Flags_A_Category_That_Does_Not_Exist_Yet()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        byte[] file = BuildWorkbook([Row(name: "Aksesuar malı", category: "Aksesuar-IMP", barcode: "IMP-CAT-1")]);

        HttpResponseMessage response = await client.PostAsync(
            "/api/imports/products/preview", MultipartFile(file));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PreviewResponseDto>())!;
        Assert.Contains("Aksesuar-IMP", body.Summary.NewCategories);
        Assert.Equal("create", body.Rows[0].Status);
    }

    [Fact]
    public async Task Preview_With_Empty_File_Returns_400_EmptyFile()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        byte[] file = BuildWorkbook([]);

        HttpResponseMessage response = await client.PostAsync(
            "/api/imports/products/preview", MultipartFile(file));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>();
        Assert.Equal("Imports.EmptyFile", error!.Code);
    }

    [Fact]
    public async Task Preview_With_More_Than_1000_Rows_Returns_400_TooManyRows()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var rows = Enumerable.Range(0, 1001).Select(i => Row(name: $"Mal {i}", barcode: $"IMP-BULK-{i}")).ToList();
        byte[] file = BuildWorkbook(rows);

        HttpResponseMessage response = await client.PostAsync(
            "/api/imports/products/preview", MultipartFile(file));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>();
        Assert.Equal("Imports.TooManyRows", error!.Code);
    }

    [Fact]
    public async Task Preview_With_Wrong_Headers_Returns_400_InvalidTemplate()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        byte[] file = BuildWorkbook([Row()], headers: ["Ad", "Qiymət", "Say"]);

        HttpResponseMessage response = await client.PostAsync(
            "/api/imports/products/preview", MultipartFile(file));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>();
        Assert.Equal("Imports.InvalidTemplate", error!.Code);
        Assert.Equal("Şablona uyğun deyil — şablonu endirib istifadə et", error.Message);
    }

    [Fact]
    public async Task Preview_Is_Forbidden_For_A_Seller()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.SellerPhone);
        byte[] file = BuildWorkbook([Row()]);

        HttpResponseMessage response = await client.PostAsync(
            "/api/imports/products/preview", MultipartFile(file));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Commit_Applies_Only_Valid_Rows_Creates_A_Category_And_Logs_One_Activity_Entry()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        await client.CreateProductAsync("IMP-COMMIT-EXIST", quantity: 3, salePrice: 10m, purchasePrice: 5m);

        byte[] file = BuildWorkbook([
            Row(name: "Commit yeni mal", category: "Idxal-Kateqoriya", barcode: "IMP-COMMIT-NEW", purchasePrice: 12, salePrice: 22, quantity: 8),
            Row(name: "Commit yenilənən mal", barcode: "IMP-COMMIT-EXIST", purchasePrice: 6, salePrice: 13, quantity: 9),
            Row(name: "Commit xətalı mal", salePrice: -1)
        ]);

        HttpResponseMessage previewResponse = await client.PostAsync(
            "/api/imports/products/preview", MultipartFile(file));
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = (await previewResponse.Content.ReadFromJsonAsync<PreviewResponseDto>())!;

        HttpResponseMessage commitResponse = await client.PostAsJsonAsync(
            "/api/imports/products/commit", new { importToken = preview.ImportToken });

        Assert.Equal(HttpStatusCode.OK, commitResponse.StatusCode);

        List<IntegrationTestHelpers.ProductDto> products =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.ProductDto>>("/api/products"))!;
        Assert.Contains(products, p => p.Name == "Commit yeni mal");
        Assert.Contains(products, p => p.Name == "Commit yenilənən mal" && p.Quantity == 9);
        Assert.DoesNotContain(products, p => p.Name == "Commit xətalı mal");

        List<CategoryDto> categories = (await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories"))!;
        Assert.Contains(categories, c => c.Name == "Idxal-Kateqoriya");

        List<IntegrationTestHelpers.ActivityDto> feed =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.ActivityDto>>("/api/activity?take=50"))!;
        Assert.Contains(feed, a => a.Detail == "Excel import: 1 yeni, 1 yenilənmə");
    }

    [Fact]
    public async Task Commit_With_An_Unknown_Token_Returns_410()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/imports/products/commit", new { importToken = "not-a-real-token" });

        Assert.Equal((HttpStatusCode)410, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>();
        Assert.Equal("Imports.TokenNotFound", error!.Code);
        Assert.Equal("Import vaxtı keçib — faylı yenidən yüklə", error.Message);
    }

    [Fact]
    public async Task Committing_The_Same_Token_Twice_Fails_The_Second_Time_With_410()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        byte[] file = BuildWorkbook([Row(name: "Bir dəfəlik idxal", barcode: "IMP-ONCE-1")]);

        HttpResponseMessage previewResponse = await client.PostAsync(
            "/api/imports/products/preview", MultipartFile(file));
        var preview = (await previewResponse.Content.ReadFromJsonAsync<PreviewResponseDto>())!;

        HttpResponseMessage first = await client.PostAsJsonAsync(
            "/api/imports/products/commit", new { importToken = preview.ImportToken });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        HttpResponseMessage second = await client.PostAsJsonAsync(
            "/api/imports/products/commit", new { importToken = preview.ImportToken });
        Assert.Equal((HttpStatusCode)410, second.StatusCode);
    }

    [Fact]
    public async Task Commit_Is_Forbidden_For_A_Seller()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.SellerPhone);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/imports/products/commit", new { importToken = "irrelevant" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_File_Built_On_The_Downloaded_Template_Is_Accepted_By_Preview()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        // The whole point of the two endpoints: what /api/exports hands out is what /api/imports accepts.
        byte[] template = await client.GetByteArrayAsync("/api/exports/products-template.xlsx");
        byte[] filled = FillDownloadedTemplate(template, [Row(name: "Şablondan mal", barcode: "IMP-TPL-1")]);

        HttpResponseMessage response = await client.PostAsync(
            "/api/imports/products/preview", MultipartFile(filled));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PreviewResponseDto>())!;
        Assert.Equal(1, body.Summary.Creates);
        Assert.Equal(0, body.Summary.Errors);
    }

    [Fact]
    public async Task Preview_Without_A_File_Part_Returns_400_EmptyFile()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsync(
            "/api/imports/products/preview", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>();
        Assert.Equal("Imports.EmptyFile", error!.Code);
    }

    [Fact]
    public async Task Preview_With_A_Non_Multipart_Body_Is_Rejected_Without_A_Server_Error()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/imports/products/preview", new { file = "not a multipart upload" });

        // The route declares multipart/form-data, so a JSON body never reaches the handler: 415, not a 500.
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    /// <summary>Drops the template's two sample rows and writes the test's own rows in their place.</summary>
    private static byte[] FillDownloadedTemplate(byte[] template, IEnumerable<object?[]> rows)
    {
        using var input = new MemoryStream(template);
        using var workbook = new XLWorkbook(input);
        IXLWorksheet sheet = workbook.Worksheet(1);

        int firstDataRow = ProductImportTemplate.HeaderRow + 1;
        while (sheet.LastRowUsed()!.RowNumber() >= firstDataRow)
            sheet.Row(sheet.LastRowUsed()!.RowNumber()).Delete();

        int rowIndex = firstDataRow;
        foreach (object?[] row in rows)
        {
            for (int c = 0; c < row.Length; c++)
                WriteCell(sheet.Cell(rowIndex, c + 1), row[c]);
            rowIndex++;
        }

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static MultipartFormDataContent MultipartFile(byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "mallar.xlsx");
        return content;
    }

    private static object?[] Row(
        string name = "Test malı",
        string category = "Test",
        string barcode = "",
        object? purchasePrice = null,
        object? salePrice = null,
        object? quantity = null,
        object? minStock = null) =>
        [
            name, category, barcode,
            purchasePrice ?? 10, salePrice ?? 20, quantity ?? 5, minStock ?? 1,
            "Anbar A", "Mərkəz", "1", "1", "", ""
        ];

    private static byte[] BuildWorkbook(IEnumerable<object?[]> rows, IReadOnlyList<string>? headers = null)
    {
        headers ??= ProductImportTemplate.Headers;
        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.Worksheets.Add("Mallar");

        for (int i = 0; i < headers.Count; i++)
            sheet.Cell(1, i + 1).Value = headers[i];

        int rowIndex = 2;
        foreach (object?[] row in rows)
        {
            for (int c = 0; c < row.Length; c++)
                WriteCell(sheet.Cell(rowIndex, c + 1), row[c]);
            rowIndex++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case string s:
                cell.Value = s;
                break;
            case int i:
                cell.Value = i;
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    private sealed record PreviewRowDto(int RowNumber, string Status, string? Error);

    private sealed record PreviewSummaryDto(int Creates, int Updates, int Errors, List<string> NewCategories);

    private sealed record PreviewResponseDto(string ImportToken, List<PreviewRowDto> Rows, PreviewSummaryDto Summary);

    private sealed record CategoryDto(Guid Id, string Name);
}
