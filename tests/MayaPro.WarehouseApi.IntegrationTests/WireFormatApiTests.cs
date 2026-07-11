using System.Net.Http.Json;
using System.Text.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// Guards the frozen frontend wire contract after the Azerbaijani→English identifier refactor. The C#
/// members are now English, but the JSON the API sends and accepts must be byte-for-byte what it was:
/// expense-breakdown keys (yol/fehle/yer/paket/diger), paymentType ("Nağd"...), category ("Yol"...) and
/// role ("sahib"...). Every assertion reads the RAW JSON, not a typed DTO, so a renamed property that
/// changed the wire key would fail here.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class WireFormatApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public WireFormatApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Product_Expense_Breakdown_Keeps_Frontend_Json_Keys_And_Values()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        // Non-zero, distinct values per bucket so a swapped mapping (e.g. fehle↔diger) would be caught.
        object body = new
        {
            name = "Wire test malı",
            category = "Test",
            size = "M",
            color = "Qara",
            model = "T-1",
            barcode = "WIRE-EXP",
            image = "",
            note = "",
            purchasePrice = 5m,
            salePrice = 10m,
            quantity = 10,
            minStock = 1,
            currency = "AZN",
            supplierId = "sup_1",
            location = "Anbar A / Rəf 1 / Qutu 1",
            store = "Anbar A",
            warehouse = "Anbar A",
            shelf = "1",
            box = "1",
            expenses = new { yol = 11m, fehle = 22m, yer = 33m, paket = 44m, diger = 55m }
        };

        HttpResponseMessage post = await client.PostAsJsonAsync("/api/products", body);
        post.EnsureSuccessStatusCode();
        var created = (await post.Content.ReadFromJsonAsync<IntegrationTestHelpers.ProductDto>())!;

        using JsonDocument doc = JsonDocument.Parse(
            await (await client.GetAsync($"/api/products/{created.Id}")).Content.ReadAsStringAsync());
        JsonElement expenses = doc.RootElement.GetProperty("expenses");

        // Keys are the frontend keys — and each value landed in the correct bucket (no swap).
        Assert.Equal(11m, expenses.GetProperty("yol").GetDecimal());
        Assert.Equal(22m, expenses.GetProperty("fehle").GetDecimal());
        Assert.Equal(33m, expenses.GetProperty("yer").GetDecimal());
        Assert.Equal(44m, expenses.GetProperty("paket").GetDecimal());
        Assert.Equal(55m, expenses.GetProperty("diger").GetDecimal());

        // The English identifiers must NOT leak onto the wire.
        Assert.False(expenses.TryGetProperty("transport", out _));
        Assert.False(expenses.TryGetProperty("labor", out _));
    }

    [Fact]
    public async Task PaymentType_Category_And_Role_Round_Trip_In_Azerbaijani()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("WIRE-ENUM", quantity: 10, salePrice: 10m);

        // paymentType stays "Nağd" on the wire.
        HttpResponseMessage sale = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 1,
            salePrice = 10m,
            discount = 0m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });
        sale.EnsureSuccessStatusCode();
        Assert.Equal("Nağd", JsonDocument.Parse(await sale.Content.ReadAsStringAsync())
            .RootElement.GetProperty("paymentType").GetString());

        // category stays "Yol" on the wire.
        HttpResponseMessage expense = await client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Wire xərci",
            category = "Yol",
            amount = 5m,
            date = (DateTime?)null,
            productId = (Guid?)null,
            note = (string?)null
        });
        expense.EnsureSuccessStatusCode();
        Assert.Equal("Yol", JsonDocument.Parse(await expense.Content.ReadAsStringAsync())
            .RootElement.GetProperty("category").GetString());

        // role stays "sahib" on the wire.
        using JsonDocument me = JsonDocument.Parse(
            await (await client.GetAsync("/api/auth/me")).Content.ReadAsStringAsync());
        Assert.Equal("sahib", me.RootElement.GetProperty("role").GetString());
    }
}
