using System.Net;
using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for day-end closing: the sales/expense totals are computed server-side, the day can
/// only be closed once, and closing is restricted to the owner.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DayEndApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public DayEndApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Close_Day_Computes_Totals_Server_Side_And_Rejects_A_Second_Close()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var product = await client.CreateProductAsync("DAYEND-P", quantity: 100, salePrice: 10m);
        var customer = await client.CreateCustomerAsync("Gün sonu müştəri", debt: 0m);

        await SellAsync(client, product.Id, quantity: 3, "Nağd");                 // +30 cash
        await SellAsync(client, product.Id, quantity: 2, "Kart");                 // +20 card
        await SellAsync(client, product.Id, quantity: 1, "Nisyə", customer.Id);   // +10 credit
        await AddGeneralExpenseAsync(client, amount: 40m);                        // +40 expenses

        // The client sends only cash figures + note; totals are the server's job.
        HttpResponseMessage close = await client.PostAsJsonAsync(
            "/api/closings", new { openingCash = 100m, actualCash = 90m, note = "Gün sonu" });

        Assert.Equal(HttpStatusCode.Created, close.StatusCode);
        var c = (await close.Content.ReadFromJsonAsync<IntegrationTestHelpers.ClosingDto>())!;

        // Server aggregated real sales/expenses (client sent none of these).
        Assert.True(c.CashSales >= 30m);
        Assert.True(c.CardSales >= 20m);
        Assert.True(c.CreditSales >= 10m);
        Assert.True(c.Expenses >= 40m);

        // Server-side maths is self-consistent.
        Assert.Equal(100m, c.OpeningCash);
        Assert.Equal(c.OpeningCash + c.CashSales - c.Expenses, c.ExpectedCash);
        Assert.Equal(c.ActualCash - c.ExpectedCash, c.Difference);

        // Second close of the same day is rejected.
        HttpResponseMessage second = await client.PostAsJsonAsync(
            "/api/closings", new { openingCash = 100m, actualCash = 90m, note = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var error = (await second.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("DayEnd.AlreadyClosed", error.Code);
    }

    [Fact]
    public async Task Seller_Cannot_Close_Day_Returns_403()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.SellerPhone);

        HttpResponseMessage close = await client.PostAsJsonAsync(
            "/api/closings", new { openingCash = 100m, actualCash = 100m, note = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, close.StatusCode);
    }

    private static Task SellAsync(HttpClient client, Guid productId, int quantity, string paymentType, Guid? customerId = null) =>
        client.PostAsJsonAsync("/api/sales", new
        {
            productId,
            quantity,
            salePrice = 10m,
            discount = 0m,
            paymentType,
            customerId
        });

    private static Task AddGeneralExpenseAsync(HttpClient client, decimal amount) =>
        client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Gün sonu xərci",
            category = "Mağaza",
            amount,
            date = (DateTime?)null,
            productId = (Guid?)null,
            note = (string?)null
        });
}
