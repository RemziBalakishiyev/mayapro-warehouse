using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for the read-only Reports module: the dashboard and the period summary are computed
/// server-side from the other modules (sales, expenses, products, customers) — Reports owns no tables.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReportsApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public ReportsApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Dashboard_Reflects_Todays_Sales_Expenses_And_Stock()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var product = await client.CreateProductAsync("RPT-DASH", quantity: 10, salePrice: 10m); // real cost 5
        await SellAsync(client, product.Id, quantity: 3, "Nağd");   // +30 sales, +15 profit
        await AddGeneralExpenseAsync(client, amount: 40m);          // +40 expenses

        var d = (await client.GetFromJsonAsync<IntegrationTestHelpers.DashboardDto>("/api/reports/dashboard"))!;

        // Shared DB accumulates across the run, so assert lower bounds from this test's own activity.
        Assert.True(d.ProductCount >= 1);
        Assert.True(d.TodaySales >= 30m);
        Assert.True(d.TodayProfit >= 15m);
        Assert.True(d.TodayExpenses >= 40m);
        Assert.True(d.TodaySalesCount >= 1);
        Assert.True(d.StockRetailValue > 0m);
        Assert.True(d.StockCostValue > 0m);
    }

    [Fact]
    public async Task Summary_Today_Aggregates_And_Is_Self_Consistent()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var product = await client.CreateProductAsync("RPT-SUM", quantity: 10, salePrice: 10m);
        await SellAsync(client, product.Id, quantity: 2, "Kart");   // +20 card sales
        await AddGeneralExpenseAsync(client, amount: 10m);

        var s = (await client.GetFromJsonAsync<IntegrationTestHelpers.SummaryDto>(
            "/api/reports/summary?period=today"))!;

        Assert.Equal("today", s.Period);
        Assert.Equal(s.From, s.To);                       // a single day
        Assert.True(s.SalesTotal >= 20m);
        Assert.True(s.CardSales >= 20m);
        Assert.True(s.Expenses >= 10m);
        Assert.Equal(s.Profit - s.Expenses, s.NetProfit); // net = profit − expenses
    }

    [Fact]
    public async Task Summary_Week_Spans_Seven_Days_And_Unknown_Period_Falls_Back_To_Today()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var week = (await client.GetFromJsonAsync<IntegrationTestHelpers.SummaryDto>(
            "/api/reports/summary?period=week"))!;
        Assert.Equal("week", week.Period);
        Assert.Equal(week.To.AddDays(-6), week.From);     // inclusive 7-day window

        var unknown = (await client.GetFromJsonAsync<IntegrationTestHelpers.SummaryDto>(
            "/api/reports/summary?period=nonsense"))!;
        Assert.Equal("today", unknown.Period);            // safe fallback
        Assert.Equal(unknown.From, unknown.To);
    }

    private static Task SellAsync(HttpClient client, Guid productId, int quantity, string paymentType) =>
        client.PostAsJsonAsync("/api/sales", new
        {
            productId,
            quantity,
            salePrice = 10m,
            discount = 0m,
            paymentType,
            customerId = (Guid?)null
        });

    private static Task AddGeneralExpenseAsync(HttpClient client, decimal amount) =>
        client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Hesabat xərci",
            category = "Mağaza",
            amount,
            date = (DateTime?)null,
            productId = (Guid?)null,
            note = (string?)null
        });
}
