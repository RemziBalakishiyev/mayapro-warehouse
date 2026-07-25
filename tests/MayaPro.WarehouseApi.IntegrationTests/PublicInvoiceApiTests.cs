using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for the public (auth-suz) invoice link: the token is minted once and stays stable,
/// the tokenised GET returns an inline PDF without authentication, and junk tokens 404.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PublicInvoiceApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public PublicInvoiceApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> CreateSaleAsync(HttpClient client, string barcode)
    {
        var product = await client.CreateProductAsync(barcode, quantity: 10, salePrice: 20m);
        HttpResponseMessage saleResponse = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 1,
            salePrice = 20m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });
        saleResponse.EnsureSuccessStatusCode();
        return (await saleResponse.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!.Id;
    }

    [Fact]
    public async Task Invoice_Link_Is_Minted_Once_And_Stable()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid saleId = await CreateSaleAsync(client, "PUB-INV-1");

        HttpResponseMessage first = await client.PostAsync($"/api/sales/{saleId}/invoice-link", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstLink = (await first.Content.ReadFromJsonAsync<InvoiceLinkDto>())!;
        Assert.Contains("/api/public/invoices/", firstLink.Url);

        // Token is the last URL segment: cryptographically random, 32+ chars, URL-safe.
        string token = firstLink.Url.Split('/').Last();
        Assert.True(token.Length >= 32, $"Token 32+ simvol olmalıdır, gəldi: {token.Length}");
        Assert.Matches("^[A-Za-z0-9_-]+$", token);

        // Second request returns the exact same link — no regeneration.
        HttpResponseMessage second = await client.PostAsync($"/api/sales/{saleId}/invoice-link", null);
        var secondLink = (await second.Content.ReadFromJsonAsync<InvoiceLinkDto>())!;
        Assert.Equal(firstLink.Url, secondLink.Url);
    }

    [Fact]
    public async Task Public_Invoice_Get_Without_Auth_Returns_Inline_Pdf()
    {
        HttpClient owner = await _factory.AuthenticatedClientAsync();
        Guid saleId = await CreateSaleAsync(owner, "PUB-INV-2");
        var link = (await (await owner.PostAsync($"/api/sales/{saleId}/invoice-link", null))
            .Content.ReadFromJsonAsync<InvoiceLinkDto>())!;

        // Anonymous client — no Authorization header; call the URL's path against the test host.
        HttpClient anonymous = _factory.CreateClient();
        string path = new Uri(link.Url).AbsolutePath;
        HttpResponseMessage response = await anonymous.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", response.Content.Headers.ContentDisposition?.DispositionType);

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Public_Invoice_With_Unknown_Token_Returns_404()
    {
        HttpClient anonymous = _factory.CreateClient();

        HttpResponseMessage response = await anonymous.GetAsync(
            "/api/public/invoices/tamamile-yanlis-token-1234567890abcdef");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Invoice_Link_For_Unknown_Sale_Returns_404()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsync($"/api/sales/{Guid.NewGuid()}/invoice-link", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Invoice_Link_Requires_Auth()
    {
        HttpClient anonymous = _factory.CreateClient();

        HttpResponseMessage response = await anonymous.PostAsync($"/api/sales/{Guid.NewGuid()}/invoice-link", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record InvoiceLinkDto(string Url);
}
