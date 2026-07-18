using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests over the real host + SQL Server test database for the Products module: the
/// create → list flow, stock adjustment, and role enforcement (a seller cannot create products).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ProductsApiTests : IAsyncLifetime
{
    private const string OwnerPhone = "0501112233";   // Sahibkar
    private const string SellerPhone = "0553334455";  // Satıcı
    private const string DemoPassword = "demo123";

    private readonly WarehouseApiFactory _factory;

    public ProductsApiTests(WarehouseApiFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Owner_Can_Create_Product_And_It_Appears_In_List()
    {
        HttpClient client = await AuthenticatedClientAsync(OwnerPhone);

        object body = NewProductBody("Test şalvar", "TSTPRD-CREATE", quantity: 30);
        HttpResponseMessage create = await client.PostAsJsonAsync("/api/products", body);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        ProductDto? created = await create.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(created);
        Assert.Equal("Test şalvar", created!.Name);
        Assert.Equal(30, created.Quantity);
        Assert.Equal(30, created.InitialQuantity);

        List<ProductDto>? all = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");
        Assert.NotNull(all);
        Assert.Contains(all!, p => p.Id == created.Id && p.Barcode == "TSTPRD-CREATE");
    }

    [Fact]
    public async Task Adjust_Stock_Changes_Quantity()
    {
        HttpClient client = await AuthenticatedClientAsync(OwnerPhone);

        // Create a product to adjust, so the test does not depend on shared seed state.
        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products", NewProductBody("Stok testi", "TSTPRD-ADJUST", quantity: 20));
        ProductDto created = (await create.Content.ReadFromJsonAsync<ProductDto>())!;

        HttpResponseMessage adjust = await client.PostAsJsonAsync(
            $"/api/products/{created.Id}/adjust-stock", new { delta = 5, note = "Sayım düzəlişi" });

        Assert.Equal(HttpStatusCode.OK, adjust.StatusCode);
        ProductDto adjusted = (await adjust.Content.ReadFromJsonAsync<ProductDto>())!;
        Assert.Equal(25, adjusted.Quantity);
    }

    [Fact]
    public async Task Seller_Cannot_Create_Product_Returns_403()
    {
        HttpClient client = await AuthenticatedClientAsync(SellerPhone);

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products", NewProductBody("İcazəsiz mal", "TSTPRD-FORBIDDEN", quantity: 5));

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string phone)
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new { phone, password = DemoPassword });
        response.EnsureSuccessStatusCode();
        LoginResponseDto login = (await response.Content.ReadFromJsonAsync<LoginResponseDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        return client;
    }

    private static object NewProductBody(string name, string barcode, int quantity) => new
    {
        name,
        category = "Test",
        attributes = new[] { new { name = "Ölçü", value = "M" }, new { name = "Rəng", value = "Qara" } },
        barcode,
        image = "",
        note = "",
        purchasePrice = 10m,
        salePrice = 20m,
        quantity,
        minStock = 2,
        currency = "AZN",
        supplierId = "sup_1",
        location = "Anbar A / Rəf 1 / Qutu 1",
        store = "Anbar A",
        warehouse = "Anbar A",
        shelf = "1",
        box = "1",
        expenses = new[] { new { name = "Yol pulu", amount = 100m } }
    };

    private sealed record LoginResponseDto(string Token);

    private sealed record ProductDto(Guid Id, string Name, string Barcode, int Quantity, int InitialQuantity);
}
