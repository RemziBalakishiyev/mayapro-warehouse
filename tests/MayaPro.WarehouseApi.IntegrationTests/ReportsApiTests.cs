using System.Net;
using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for the read-only Reports module: the dashboard and the period summary are computed
/// server-side from the other modules (sales, expenses, products, customers, suppliers, day-end) —
/// Reports owns no tables.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReportsApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public ReportsApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Dashboard_Reflects_Trading_Stock_Debts_And_Frozen_Groups()
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

        // Extended fields.
        Assert.True(d.TotalSupplierDebt >= 0m);   // "my debts" (ISuppliersModule.GetTotalDebtAsync)

        // Frozen buckets are cumulative: 30-day count ≥ 60-day ≥ 90-day. Seeded, never-sold in-stock
        // products count as frozen in every bucket, so the 90-day group is non-empty.
        Assert.NotNull(d.FrozenProducts);
        Assert.True(d.FrozenProducts.Days30 >= d.FrozenProducts.Days60);
        Assert.True(d.FrozenProducts.Days60 >= d.FrozenProducts.Days90);
        Assert.True(d.FrozenProducts.Days90 >= 1);

        // Top products: at least one sold, each with a positive quantity, capped at five.
        Assert.NotNull(d.TopProducts);
        Assert.NotEmpty(d.TopProducts);
        Assert.True(d.TopProducts.Count <= 5);
        Assert.All(d.TopProducts, t => Assert.True(t.QuantitySold > 0));

        // Low-stock list is consistent with the count.
        Assert.NotNull(d.LowStock);
        Assert.Equal(d.LowStockCount, d.LowStock.Count);

        // Frozen products now carry detail rows; the seeded never-sold items appear with null idle days.
        Assert.NotEmpty(d.FrozenProducts.Items);
        Assert.Contains(d.FrozenProducts.Items, i => i.DaysSinceLastSale == null && i.FrozenValue > 0m);

        // Trend series are fixed-length and end today; today's point reflects this test's sale.
        Assert.Equal(14, d.DailySeries.Count);
        Assert.Equal(6, d.MonthlySeries.Count);
        Assert.True(d.DailySeries[^1].Sales >= 30m);

        // Recent activity feeds are present and reflect the sale just made.
        Assert.NotEmpty(d.RecentSales);
        Assert.Contains(d.RecentSales, r => r.PaymentType == "Nağd");
        Assert.NotNull(d.RecentPayments);
    }

    [Fact]
    public async Task Dashboard_RecentSales_Include_CustomerName_For_Credit_Sale()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var product = await client.CreateProductAsync("RPT-CREDIT", quantity: 10, salePrice: 10m);
        var customer = await client.CreateCustomerAsync("Nisyə müştəri");
        await SellOnCreditAsync(client, product.Id, quantity: 1, customer.Id);

        var d = (await client.GetFromJsonAsync<IntegrationTestHelpers.DashboardDto>("/api/reports/dashboard"))!;

        // The credit sale was the most recent, so it appears in the feed with its customer's name resolved.
        Assert.Contains(d.RecentSales, r => r.PaymentType == "Nisyə" && r.CustomerName == "Nisyə müştəri");
        // Cash/card sales stay anonymous — no customer name.
        Assert.All(d.RecentSales.Where(r => r.PaymentType != "Nisyə"), r => Assert.Null(r.CustomerName));
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
    public async Task Summary_Week_Spans_Seven_Days_And_All_Is_Unbounded()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var product = await client.CreateProductAsync("RPT-ALL", quantity: 5, salePrice: 10m);
        await SellAsync(client, product.Id, quantity: 1, "Nağd");

        var week = (await client.GetFromJsonAsync<IntegrationTestHelpers.SummaryDto>(
            "/api/reports/summary?period=week"))!;
        Assert.Equal("week", week.Period);
        Assert.NotNull(week.From);
        Assert.NotNull(week.To);
        Assert.Equal(week.To!.Value.AddDays(-6), week.From!.Value); // inclusive 7-day window

        var all = (await client.GetFromJsonAsync<IntegrationTestHelpers.SummaryDto>(
            "/api/reports/summary?period=all"))!;
        Assert.Equal("all", all.Period);
        Assert.Null(all.From);            // unbounded — whole history
        Assert.Null(all.To);
        Assert.True(all.SalesTotal >= 10m);
    }

    [Fact]
    public async Task Summary_Unknown_Period_Returns_400()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage resp = await client.GetAsync("/api/reports/summary?period=nonsense");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var error = (await resp.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Reports.InvalidPeriod", error.Code);
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

    private static Task SellOnCreditAsync(HttpClient client, Guid productId, int quantity, Guid customerId) =>
        client.PostAsJsonAsync("/api/sales", new
        {
            productId,
            quantity,
            salePrice = 10m,
            discount = 0m,
            paymentType = "Nisyə",
            customerId = (Guid?)customerId
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
