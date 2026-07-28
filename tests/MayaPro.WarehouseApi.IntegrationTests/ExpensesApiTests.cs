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
            category = "Yol pulu",
            source = "product",
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
            category = "Mağaza xərci",
            source = "general",
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
            category = "Yol pulu",
            source = "product",
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
            category = "Yol pulu",
            source = "product",
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

    [Fact]
    public async Task Switching_A_Product_Expense_To_General_Gives_The_Products_Cost_Back()
    {
        // The edit path is the second way an expense can reach maya. Re-pointing it at "general" must
        // unwind the old effect and add nothing back — end-to-end proof of AC-4 on the update path.
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("EXP-SRC-SWITCH", quantity: 10, salePrice: 20m);
        Assert.Equal(5.00m, (await client.GetProductAsync(product.Id)).RealCostPerUnit);

        var expense = await CreateExpenseAsync(client, product.Id, amount: 100m); // → 15.00
        Assert.Equal(15.00m, (await client.GetProductAsync(product.Id)).RealCostPerUnit);

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/expenses/{expense.Id}", new
        {
            title = "Mağaza icarəsi",
            category = "Mağaza xərci",
            source = "general",
            amount = 100m,
            date = (DateTime?)null,
            productId = (Guid?)null,
            note = (string?)null
        });

        decimal afterCost = (await client.GetProductAsync(product.Id)).RealCostPerUnit;
        if (update.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal(5.00m, afterCost); // back to the untouched cost
            var dto = (await update.Content.ReadFromJsonAsync<IntegrationTestHelpers.ExpenseDto>())!;
            Assert.Equal("general", dto.Source);
            Assert.Null(dto.ProductId);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
            Assert.Equal(15.00m, afterCost); // closed-day guard held
        }
    }

    [Fact]
    public async Task Product_Source_Without_ProductId_Is_Rejected_With_400()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Yanlış xərc",
            category = "Yol pulu",
            source = "product",
            amount = 10m,
            date = (DateTime?)null,
            productId = (Guid?)null,
            note = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task General_Source_With_ProductId_Is_Rejected_With_400()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("EXP-GEN-BAD", quantity: 10, salePrice: 20m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Yanlış xərc",
            category = "Mağaza xərci",
            source = "general",
            amount = 10m,
            date = (DateTime?)null,
            productId = product.Id,
            note = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Source_Filter_Returns_Only_Matching_Expenses_For_The_Month()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("EXP-SRC-FILTER", quantity: 10, salePrice: 20m);
        string month = DateTime.UtcNow.ToString("yyyy-MM");

        await CreateExpenseAsync(client, product.Id, amount: 10m);
        await CreateExpenseAsync(client, product.Id, amount: 20m);
        await CreateGeneralExpenseAsync(client, amount: 5m);

        List<IntegrationTestHelpers.ExpenseDto> generalOnly =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.ExpenseDto>>(
                $"/api/expenses?source=general&month={month}"))!;
        Assert.All(generalOnly, e => Assert.Equal("general", e.Source));
        Assert.Contains(generalOnly, e => e.Amount == 5m);
        Assert.DoesNotContain(generalOnly, e => e.Amount is 10m or 20m);

        List<IntegrationTestHelpers.ExpenseDto> productOnly =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.ExpenseDto>>(
                $"/api/expenses?source=product&month={month}"))!;
        Assert.All(productOnly, e => Assert.Equal("product", e.Source));
        Assert.Contains(productOnly, e => e.Amount == 10m);
        Assert.Contains(productOnly, e => e.Amount == 20m);
        Assert.DoesNotContain(productOnly, e => e.Amount == 5m);
    }

    [Fact]
    public async Task Unknown_Source_Filter_Does_Not_Return_500()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.GetAsync("/api/expenses?source=unknown");

        // Documented choice (task TC-12): an unrecognised source is a validation error, not a 500.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Expenses.InvalidSource", error.Code);
    }

    private static async Task<IntegrationTestHelpers.ExpenseDto> CreateExpenseAsync(
        HttpClient client, Guid productId, decimal amount)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Karqo",
            category = "Yol pulu",
            source = "product",
            amount,
            date = (DateTime?)null,
            productId,
            note = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ExpenseDto>())!;
    }

    private static async Task<IntegrationTestHelpers.ExpenseDto> CreateGeneralExpenseAsync(
        HttpClient client, decimal amount)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Mağaza xərci",
            category = "Mağaza xərci",
            source = "general",
            amount,
            date = (DateTime?)null,
            productId = (Guid?)null,
            note = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ExpenseDto>())!;
    }
}
