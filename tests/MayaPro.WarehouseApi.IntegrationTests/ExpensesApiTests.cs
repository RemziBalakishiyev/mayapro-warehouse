using System.Net;
using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for the expense → product real-cost chain: a product-linked expense raises exactly
/// that product's real cost, a general expense changes no product, and a non-existent product rolls the
/// whole thing back (no expense written).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ExpensesApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public ExpensesApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Product_Linked_Expense_Increases_That_Products_Real_Cost()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        // purchasePrice 5, no expenses, initialQuantity 10 → realCost 5.00.
        var product = await client.CreateProductAsync("EXP-COST", quantity: 10, salePrice: 20m);
        var before = await client.GetProductAsync(product.Id);
        Assert.Equal(5.00m, before.RealCostPerUnit);
        Assert.Equal(10, before.InitialQuantity);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Karqo",
            category = "Yol",
            amount = 100m,
            date = (DateTime?)null,
            productId = product.Id,
            note = (string?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Exact check: old real cost + amount / initialQuantity = 5 + 100/10 = 15.00.
        var after = await client.GetProductAsync(product.Id);
        Assert.Equal(before.RealCostPerUnit + 100m / before.InitialQuantity, after.RealCostPerUnit);
        Assert.Equal(15.00m, after.RealCostPerUnit);
    }

    [Fact]
    public async Task General_Expense_Does_Not_Change_Any_Product_Cost()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("EXP-GENERAL", quantity: 10, salePrice: 20m);
        var before = await client.GetProductAsync(product.Id);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Mağaza icarəsi",
            category = "Mağaza",
            amount = 600m,
            date = (DateTime?)null,
            productId = (Guid?)null,
            note = (string?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var after = await client.GetProductAsync(product.Id);
        Assert.Equal(before.RealCostPerUnit, after.RealCostPerUnit);
    }

    [Fact]
    public async Task Expense_For_Nonexistent_Product_Returns_404_And_Writes_No_Expense()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        const string title = "Rollback xərci (yazılmamalı)";
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/expenses", new
        {
            title,
            category = "Yol",
            amount = 100m,
            date = (DateTime?)null,
            productId = Guid.NewGuid(), // does not exist
            note = (string?)null
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Products.NotFound", error.Code);

        // Rollback proof: the expense was not persisted.
        List<IntegrationTestHelpers.ExpenseDto> all =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.ExpenseDto>>("/api/expenses"))!;
        Assert.DoesNotContain(all, e => e.Title == title);
    }
}
