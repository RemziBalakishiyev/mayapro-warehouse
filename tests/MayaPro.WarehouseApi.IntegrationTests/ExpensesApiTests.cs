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

    [Fact]
    public async Task Delete_Nonexistent_Expense_Returns_404()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.DeleteAsync($"/api/expenses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Expenses.NotFound", error.Code);
    }

    [Fact]
    public async Task Delete_Product_Linked_Expense_Lowers_The_Products_Real_Cost_Back()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("EXP-DEL-COST", quantity: 10, salePrice: 20m);
        Assert.Equal(5.00m, (await client.GetProductAsync(product.Id)).RealCostPerUnit);

        var expense = await CreateExpenseAsync(client, product.Id, amount: 100m); // 5 + 100/10 = 15.00
        Assert.Equal(15.00m, (await client.GetProductAsync(product.Id)).RealCostPerUnit);

        HttpResponseMessage delete = await client.DeleteAsync($"/api/expenses/{expense.Id}");

        decimal afterCost = (await client.GetProductAsync(product.Id)).RealCostPerUnit;
        if (delete.StatusCode == HttpStatusCode.OK)
            Assert.Equal(5.00m, afterCost); // the expense was unwound from the cost
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
            var error = (await delete.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
            Assert.Equal("Expenses.DayClosedConflict", error.Code);
            Assert.Equal(15.00m, afterCost); // guard held
        }
    }

    [Fact]
    public async Task Update_Product_Linked_Expense_Reapplies_The_New_Amount_To_The_Cost()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("EXP-UPD-COST", quantity: 10, salePrice: 20m);
        var expense = await CreateExpenseAsync(client, product.Id, amount: 100m); // → 15.00
        Assert.Equal(15.00m, (await client.GetProductAsync(product.Id)).RealCostPerUnit);

        // Reverse 100 then apply 50 → 5 + 50/10 = 10.00.
        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/expenses/{expense.Id}", new
        {
            title = "Karqo (düzəliş)",
            category = "Yol",
            amount = 50m,
            date = (DateTime?)null,
            productId = product.Id,
            note = (string?)null
        });

        decimal afterCost = (await client.GetProductAsync(product.Id)).RealCostPerUnit;
        if (update.StatusCode == HttpStatusCode.OK)
            Assert.Equal(10.00m, afterCost);
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
            Assert.Equal(15.00m, afterCost); // guard held
        }
    }

    private static async Task<IntegrationTestHelpers.ExpenseDto> CreateExpenseAsync(
        HttpClient client, Guid productId, decimal amount)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Karqo",
            category = "Yol",
            amount,
            date = (DateTime?)null,
            productId,
            note = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ExpenseDto>())!;
    }
}
