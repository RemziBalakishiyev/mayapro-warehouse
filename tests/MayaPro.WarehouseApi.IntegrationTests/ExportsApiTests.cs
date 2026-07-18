using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;

namespace MayaPro.WarehouseApi.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ExportsApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public ExportsApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Products_Excel_Returns_Workbook_With_Product_Rows()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        await client.CreateProductAsync("EXP-XLSX-1", quantity: 10, salePrice: 12m);
        await client.CreateProductAsync("EXP-XLSX-2", quantity: 5, salePrice: 8m);

        HttpResponseMessage response = await client.GetAsync("/api/exports/products.xlsx");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.StartsWith("mallar-", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        IXLWorksheet sheet = workbook.Worksheet(1);
        // Row 1 = store/date, row 2 = headers, rows 3+ = products (+ any seed catalogue rows).
        int lastRow = sheet.LastRowUsed()!.RowNumber();
        int dataRows = lastRow - 2;
        Assert.True(dataRows >= 2, $"Expected at least 2 product data rows, got {dataRows}");
        Assert.Equal("Ad", sheet.Cell(2, 1).GetString());
    }

    [Fact]
    public async Task Sales_Pdf_Returns_Pdf_With_Magic_Bytes()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("EXP-PDF-1", quantity: 20, salePrice: 10m);
        HttpResponseMessage saleResponse = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 2,
            salePrice = 10m,
            discount = 0m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });
        saleResponse.EnsureSuccessStatusCode();

        HttpResponseMessage response = await client.GetAsync("/api/exports/sales.pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.StartsWith("satislar-", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 5120, $"PDF should be > 5KB, was {bytes.Length}");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Seller_Can_Export_Products_Excel()
    {
        HttpClient owner = await _factory.AuthenticatedClientAsync();
        await owner.CreateProductAsync("EXP-SELLER-1", quantity: 3, salePrice: 15m);

        HttpClient seller = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.SellerPhone);
        HttpResponseMessage response = await seller.GetAsync("/api/exports/products.xlsx");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
    }
}
