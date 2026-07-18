using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for the managed categories endpoint and the dynamic product-attributes round-trip:
/// create a category → it appears in the list → a duplicate is rejected with 400; and a product created
/// with attributes returns them from GET.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CategoriesApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public CategoriesApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_Category_Then_It_Appears_In_List_And_Duplicate_Is_Rejected()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        const string name = "İnteqrasiya kateqoriyası";

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/categories", new { name });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        CategoryDto? created = await create.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.NotNull(created);
        Assert.Equal(name, created!.Name);

        // It shows up in the list, ordered by name.
        List<CategoryDto>? all = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        Assert.NotNull(all);
        Assert.Contains(all!, c => c.Id == created.Id && c.Name == name);

        // A second one with the same name is a duplicate → 400 with the Azerbaijani message.
        HttpResponseMessage duplicate = await client.PostAsJsonAsync("/api/categories", new { name });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        using JsonDocument error = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        Assert.Equal("Bu kateqoriya artıq mövcuddur", error.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Empty_Category_Name_Is_Rejected_With_400()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/categories", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task Seller_Can_Also_Create_A_Category()
    {
        // By product decision, creating a category is open to every role (unlike creating a product).
        HttpClient client = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.SellerPhone);

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/categories", new { name = "Satıcı kateqoriyası" });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    [Fact]
    public async Task Product_Created_With_Attributes_Returns_Them_From_Get()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        object body = new
        {
            name = "Atributlu mal",
            category = "Test",
            attributes = new[]
            {
                new { name = "Ölçü", value = "L" },
                new { name = "Rəng", value = "Yaşıl" },
                new { name = "Material", value = "Pambıq" }
            },
            barcode = "ATTR-RT",
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
            expenses = Array.Empty<object>()
        };

        HttpResponseMessage post = await client.PostAsJsonAsync("/api/products", body);
        post.EnsureSuccessStatusCode();
        var created = (await post.Content.ReadFromJsonAsync<ProductWithAttributesDto>())!;

        // Returned straight from the create response...
        Assert.Equal(3, created.Attributes.Count);
        Assert.Equal("Ölçü", created.Attributes[0].Name);
        Assert.Equal("L", created.Attributes[0].Value);

        // ...and again after a fresh GET (proves the JSON column round-trips through the database).
        var fetched = (await client.GetFromJsonAsync<ProductWithAttributesDto>($"/api/products/{created.Id}"))!;
        Assert.Equal(3, fetched.Attributes.Count);
        Assert.Equal("Material", fetched.Attributes[2].Name);
        Assert.Equal("Pambıq", fetched.Attributes[2].Value);
    }

    private sealed record CategoryDto(Guid Id, string Name);

    private sealed record ProductWithAttributesDto(Guid Id, string Name, List<AttributeDto> Attributes);

    private sealed record AttributeDto(string Name, string Value);
}
