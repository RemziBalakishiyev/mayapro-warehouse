using System.Net;
using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for the expense → product real-cost chain: a product-linked expense raises exactly
/// that product's real cost, a general expense changes no product, and a non-existent product rolls the
/// whole thing back (no expense written). Also covers the server-side date rule (BE#9) — a future date is
/// rejected with 400 on both create and update.
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

    [Fact]
    public async Task Future_Dated_Expense_Returns_400_And_Writes_No_Expense()
    {
        // BE#9 / TC-01 over the wire: the rule must live on the server, not only in the form.
        HttpClient client = await _factory.AuthenticatedClientAsync();

        const string title = "Gələcək xərci (yazılmamalı)";
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/expenses", new
        {
            title,
            category = "Yol",
            amount = 100m,
            date = DateTime.UtcNow.AddDays(2), // future in every time zone
            productId = (Guid?)null,
            note = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("General.Validation", error.Code);
        Assert.Equal("Xərcin tarixi gələcək ola bilməz", error.Message);

        List<IntegrationTestHelpers.ExpenseDto> all =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.ExpenseDto>>("/api/expenses"))!;
        Assert.DoesNotContain(all, e => e.Title == title);
    }

    [Fact]
    public async Task Today_Dated_Expense_Is_Accepted()
    {
        // TC-02/TC-04: an explicit "now" is today in Baku whatever the UTC hour is — it must not be rejected.
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Bugünkü xərc",
            category = "Yol",
            amount = 10m,
            date = DateTime.UtcNow,
            productId = (Guid?)null,
            note = (string?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Update_To_A_Future_Date_Returns_400()
    {
        // AC-2: editing obeys the same rule. Validation runs before the closed-day guard, so this is 400,
        // never 409.
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("EXP-FUTURE-UPD", quantity: 10, salePrice: 20m);
        var expense = await CreateExpenseAsync(client, product.Id, amount: 100m);

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/expenses/{expense.Id}", new
        {
            title = "Karqo (gələcəyə köçürülmüş)",
            category = "Yol",
            amount = 100m,
            date = DateTime.UtcNow.AddDays(2),
            productId = product.Id,
            note = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        var error = (await update.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Xərcin tarixi gələcək ola bilməz", error.Message);
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
