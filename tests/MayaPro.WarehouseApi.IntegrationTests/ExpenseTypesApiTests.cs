using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for the managed expense types endpoint: create a type → it appears in the list → a
/// duplicate (case-insensitive) is rejected with 400; a blank name is rejected. AC-1, AC-2, AC-11.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ExpenseTypesApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public ExpenseTypesApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_ExpenseType_Then_It_Appears_In_List_And_Duplicate_Is_Rejected()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        const string name = "İnteqrasiya xərc növü";

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/expense-types", new { name });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        ExpenseTypeDto? created = await create.Content.ReadFromJsonAsync<ExpenseTypeDto>();
        Assert.NotNull(created);
        Assert.Equal(name, created!.Name);

        // It shows up in the list, ordered by name.
        List<ExpenseTypeDto>? all = await client.GetFromJsonAsync<List<ExpenseTypeDto>>("/api/expense-types");
        Assert.NotNull(all);
        Assert.Contains(all!, t => t.Id == created.Id && t.Name == name);

        // A second one with the same name is a duplicate → 400 with the Azerbaijani message.
        HttpResponseMessage duplicate = await client.PostAsJsonAsync("/api/expense-types", new { name });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        using JsonDocument error = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        Assert.Equal("Bu xərc növü artıq mövcuddur", error.RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_ExpenseType_Name_Is_Rejected_With_400(string name)
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/expense-types", new { name });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task Seeded_Default_Types_Are_Present()
    {
        // AC-3: the host seeds these seven on startup (Development, once per test run). The shared test DB
        // accumulates across the run, so assert presence rather than an exact count.
        HttpClient client = await _factory.AuthenticatedClientAsync();

        List<ExpenseTypeDto>? all = await client.GetFromJsonAsync<List<ExpenseTypeDto>>("/api/expense-types");
        Assert.NotNull(all);

        string[] expected =
        [
            "Yol pulu", "Fəhlə pulu", "Yer/Anbar xərci", "Paket/Qutu", "Gömrük", "Mağaza xərci", "Digər"
        ];
        foreach (string name in expected)
            Assert.Contains(all!, t => t.Name == name);
    }

    [Fact]
    public async Task Seller_Can_Also_Create_An_ExpenseType()
    {
        // Same product decision as managed categories: any authenticated role may add an expense type.
        HttpClient client = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.SellerPhone);

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/expense-types", new { name = "Satıcı xərc növü" });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    private sealed record ExpenseTypeDto(Guid Id, string Name);
}
