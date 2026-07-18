using System.Net.Http.Json;
using System.Text.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// Guards the frontend wire contract. Most of it is frozen (expense-breakdown keys yol/fehle/yer/paket/diger,
/// paymentType "Nağd"..., category "Yol"..., role "sahib"...): those must stay byte-for-byte. Every assertion
/// reads RAW JSON, not a typed DTO, so a renamed property that changed a frozen wire key would fail here.
/// <para>
/// DELIBERATE CONTRACT CHANGE (agreed with the frontend): the fixed product fields <c>size</c>/<c>color</c>/
/// <c>model</c> are GONE, replaced by a dynamic <c>attributes: [{ name, value }]</c> array (camelCase), and a
/// new managed <c>GET/POST /api/categories</c> endpoint is added. The tests below assert the NEW shape on
/// purpose — if this file is regenerated from the old contract it should fail.
/// </para>
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
            attributes = new[] { new { name = "Ölçü", value = "M" } },
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

    /// <summary>
    /// The NEW product shape: dynamic <c>attributes: [{ name, value }]</c> (camelCase) instead of the removed
    /// size/color/model fields. Sending size/color/model must have no effect, and they must never appear on
    /// the wire in the response.
    /// </summary>
    [Fact]
    public async Task Product_Attributes_Replace_Size_Color_Model_As_A_CamelCase_Array()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        object body = new
        {
            name = "Attributes wire malı",
            category = "Test",
            attributes = new[]
            {
                new { name = "Ölçü", value = "42-44" },
                new { name = "Rəng", value = "Qırmızı" }
            },
            // Old fields sent on purpose — the API must ignore them (they are no longer part of the contract).
            size = "SHOULD-BE-IGNORED",
            color = "SHOULD-BE-IGNORED",
            model = "SHOULD-BE-IGNORED",
            barcode = "WIRE-ATTR",
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
            expenses = new { yol = 0m, fehle = 0m, yer = 0m, paket = 0m, diger = 0m }
        };

        HttpResponseMessage post = await client.PostAsJsonAsync("/api/products", body);
        post.EnsureSuccessStatusCode();
        var created = (await post.Content.ReadFromJsonAsync<IntegrationTestHelpers.ProductDto>())!;

        using JsonDocument doc = JsonDocument.Parse(
            await (await client.GetAsync($"/api/products/{created.Id}")).Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;

        JsonElement attributes = root.GetProperty("attributes");
        Assert.Equal(JsonValueKind.Array, attributes.ValueKind);
        Assert.Equal(2, attributes.GetArrayLength());

        Assert.Equal("Ölçü", attributes[0].GetProperty("name").GetString());
        Assert.Equal("42-44", attributes[0].GetProperty("value").GetString());
        Assert.Equal("Rəng", attributes[1].GetProperty("name").GetString());
        Assert.Equal("Qırmızı", attributes[1].GetProperty("value").GetString());

        // The removed fields must not appear on the wire, and the ignored input must not have leaked in.
        Assert.False(root.TryGetProperty("size", out _));
        Assert.False(root.TryGetProperty("color", out _));
        Assert.False(root.TryGetProperty("model", out _));
    }

    /// <summary>NEW endpoint: a category created via POST comes back in the GET list (objects with id + name).</summary>
    [Fact]
    public async Task Categories_Endpoint_Lists_And_Creates_Categories()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage post = await client.PostAsJsonAsync("/api/categories", new { name = "Wire kateqoriya" });
        post.EnsureSuccessStatusCode();

        using JsonDocument list = JsonDocument.Parse(
            await (await client.GetAsync("/api/categories")).Content.ReadAsStringAsync());
        JsonElement root = list.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        bool found = root.EnumerateArray().Any(c =>
            c.GetProperty("name").GetString() == "Wire kateqoriya" &&
            c.TryGetProperty("id", out _));
        Assert.True(found);
    }

    /// <summary>
    /// <summary>
    /// DELIBERATE CONTRACT CHANGE (agreed with the frontend): the <c>Sale</c> wire shape now supports free-form
    /// ("manual") sales. <c>productId</c> becomes nullable, a boolean <c>isManual</c> is added, and
    /// <c>costPerUnit</c>/<c>profit</c> become nullable — for a manual sale with no cost they are JSON <c>null</c>
    /// (unknown), never coerced to 0. A nullable <c>category</c> snapshot is also on the wire; omitted on a
    /// manual sale → JSON <c>null</c>. The assertions below pin the NEW shape on purpose.
    /// </summary>
    [Fact]
    public async Task Manual_Sale_Exposes_Null_Product_And_Profit_With_IsManual_Flag()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage sale = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = (Guid?)null,
            productName = "Sərbəst mal",
            quantity = 2,
            salePrice = 12.5m,
            discount = 0m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });
        sale.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await sale.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;

        Assert.Equal(JsonValueKind.Null, root.GetProperty("productId").ValueKind);
        Assert.True(root.GetProperty("isManual").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("costPerUnit").ValueKind);   // cost unknown → null, not 0
        Assert.Equal(JsonValueKind.Null, root.GetProperty("profit").ValueKind);        // profit unknown → null, not 0
        Assert.Equal(JsonValueKind.Null, root.GetProperty("category").ValueKind);      // category omitted → null
        Assert.Equal("Sərbəst mal", root.GetProperty("productName").GetString());
        Assert.Equal(25m, root.GetProperty("totalAmount").GetDecimal());               // revenue is still recorded
        Assert.Equal("Nağd", root.GetProperty("paymentType").GetString());
    }

    /// <summary>A manual sale WITH a cost reports a real, computed profit — the field is nullable, not absent.</summary>
    [Fact]
    public async Task Manual_Sale_With_Cost_Reports_Computed_Profit_On_The_Wire()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage sale = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = (Guid?)null,
            productName = "Sərbəst maya bilinən",
            quantity = 2,
            salePrice = 20m,
            discount = 0m,
            costPerUnit = 12m,   // cost known → profit computes: (20-12)*2 = 16
            paymentType = "Nağd",
            customerId = (Guid?)null
        });
        sale.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await sale.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;

        Assert.True(root.GetProperty("isManual").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("productId").ValueKind);
        Assert.Equal(12m, root.GetProperty("costPerUnit").GetDecimal());
        Assert.Equal(16m, root.GetProperty("profit").GetDecimal());
    }

    /// <summary>
    /// DELIBERATE CONTRACT CHANGE: a catalogued sale snapshots the product's <c>category</c> onto the sale
    /// wire (and GetSales / dashboard recentSales). CreateProductAsync seeds category "Test".
    /// </summary>
    [Fact]
    public async Task Catalogued_Sale_Snapshots_Product_Category_On_The_Wire()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("WIRE-CAT-SNAP", quantity: 5, salePrice: 10m);

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 1,
            salePrice = 10m,
            discount = 0m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });
        created.EnsureSuccessStatusCode();

        using JsonDocument saleDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("Test", saleDoc.RootElement.GetProperty("category").GetString());
        Assert.False(saleDoc.RootElement.GetProperty("isManual").GetBoolean());

        // GetSales also returns the snapshot.
        using JsonDocument listDoc = JsonDocument.Parse(
            await (await client.GetAsync("/api/sales")).Content.ReadAsStringAsync());
        JsonElement match = listDoc.RootElement.EnumerateArray()
            .First(s => s.GetProperty("id").GetGuid() == saleDoc.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Test", match.GetProperty("category").GetString());

        // Dashboard recentSales carries the same snapshot.
        using JsonDocument dashDoc = JsonDocument.Parse(
            await (await client.GetAsync("/api/reports/dashboard")).Content.ReadAsStringAsync());
        JsonElement recent = dashDoc.RootElement.GetProperty("recentSales").EnumerateArray()
            .First(s => s.GetProperty("id").GetGuid() == saleDoc.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Test", recent.GetProperty("category").GetString());
    }

    /// <summary>
    /// DELIBERATE CONTRACT CHANGE: a free-form sale may send an optional <c>category</c>; when supplied it is
    /// stored as-is on the sale wire.
    /// </summary>
    [Fact]
    public async Task Manual_Sale_Persists_Supplied_Category_On_The_Wire()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage sale = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = (Guid?)null,
            productName = "Sərbəst kateqoriyalı",
            category = "Aksesuar",
            quantity = 1,
            salePrice = 8m,
            discount = 0m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });
        sale.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await sale.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;

        Assert.True(root.GetProperty("isManual").GetBoolean());
        Assert.Equal("Aksesuar", root.GetProperty("category").GetString());
    }

    [Fact]
    public async Task PaymentType_Category_And_Role_Round_Trip_In_Azerbaijani()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("WIRE-ENUM", quantity: 10, salePrice: 10m);

        // paymentType stays "Nağd" on the wire; category is snapshotted from the product ("Test").
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
        using JsonDocument saleDoc = JsonDocument.Parse(await sale.Content.ReadAsStringAsync());
        Assert.Equal("Nağd", saleDoc.RootElement.GetProperty("paymentType").GetString());
        Assert.Equal("Test", saleDoc.RootElement.GetProperty("category").GetString());

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
