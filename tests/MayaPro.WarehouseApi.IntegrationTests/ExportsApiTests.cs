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
    public async Task Sale_Invoice_Pdf_Returns_Pdf_For_Cash_Sale()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("EXP-INV-1", quantity: 10, salePrice: 25m);
        HttpResponseMessage saleResponse = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 2,
            salePrice = 25m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });
        saleResponse.EnsureSuccessStatusCode();
        var sale = (await saleResponse.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;

        HttpResponseMessage response = await client.GetAsync($"/api/exports/sales/{sale.Id}/invoice.pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        string? fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        Assert.StartsWith("faktura-SF-", fileName);
        Assert.EndsWith(".pdf", fileName);

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 5120, $"PDF should be > 5KB, was {bytes.Length}");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Sale_Invoice_Pdf_Returns_Pdf_For_Credit_Sale_With_Customer()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("EXP-INV-2", quantity: 10, salePrice: 40m);
        var customer = await client.CreateCustomerAsync("Faktura Müştərisi", debt: 15m);
        HttpResponseMessage saleResponse = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 1,
            salePrice = 40m,
            paymentType = "Nisyə",
            customerId = customer.Id
        });
        saleResponse.EnsureSuccessStatusCode();
        var sale = (await saleResponse.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;

        HttpResponseMessage response = await client.GetAsync($"/api/exports/sales/{sale.Id}/invoice.pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Sale_Invoice_Pdf_Returns_404_For_Unknown_Sale()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.GetAsync($"/api/exports/sales/{Guid.NewGuid()}/invoice.pdf");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Seller_Can_Export_Sale_Invoice_Pdf()
    {
        HttpClient owner = await _factory.AuthenticatedClientAsync();
        var product = await owner.CreateProductAsync("EXP-INV-3", quantity: 5, salePrice: 12m);

        HttpClient seller = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.SellerPhone);
        HttpResponseMessage saleResponse = await seller.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 1,
            salePrice = 12m,
            paymentType = "Kart",
            customerId = (Guid?)null
        });
        saleResponse.EnsureSuccessStatusCode();
        var sale = (await saleResponse.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;

        HttpResponseMessage response = await seller.GetAsync($"/api/exports/sales/{sale.Id}/invoice.pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
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

    [Fact]
    public async Task Labels_Pdf_Returns_Pdf_With_Magic_Bytes_For_Barcoded_Products()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("EXP-LABEL-BARCODE-1", quantity: 20, salePrice: 12.5m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/exports/products/labels.pdf", new
        {
            items = new[] { new { productId = product.Id, count = 10 } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.StartsWith("etiketler-", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Labels_Pdf_Renders_Qr_Codes_When_Type_Is_Qr()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("EXP-LABEL-QR-1", quantity: 5, salePrice: 3m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/exports/products/labels.pdf", new
        {
            items = new[] { new { productId = product.Id, count = 1 } },
            type = "qr"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Labels_Pdf_Returns_400_When_A_Product_Has_No_Barcode()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("", quantity: 5, salePrice: 4m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/exports/products/labels.pdf", new
        {
            items = new[] { new { productId = product.Id, count = 2 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>();
        Assert.Equal("Exports.ProductsWithoutBarcode", error!.Code);
        Assert.Contains("barkodu yoxdur", error.Message);
        Assert.Contains("Satış test malı", error.Message); // the fixed test-fixture product name
    }

    [Fact]
    public async Task Labels_Pdf_Returns_400_When_Total_Count_Exceeds_500()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("EXP-LABEL-TOOMANY-1", quantity: 999, salePrice: 6m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/exports/products/labels.pdf", new
        {
            items = new[] { new { productId = product.Id, count = 501 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>();
        Assert.Equal("Exports.TooManyLabels", error!.Code);
    }
}
