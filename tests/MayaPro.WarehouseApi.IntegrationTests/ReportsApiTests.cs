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
    public async Task Manual_Sale_Without_Cost_Counts_In_Sales_But_Reports_Unknown_Profit()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        // Free-form cash sale, no cost supplied → revenue is real, profit is unknown.
        HttpResponseMessage sale = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = (Guid?)null,
            productName = "Sərbəst mal",
            quantity = 1,
            salePrice = 77m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });
        sale.EnsureSuccessStatusCode();

        var d = (await client.GetFromJsonAsync<IntegrationTestHelpers.DashboardDto>("/api/reports/dashboard"))!;
        Assert.True(d.TodaySales >= 77m);                    // revenue counts toward sales
        Assert.True(d.UnknownProfitSalesCount >= 1);         // …but is flagged as unknown-profit
        Assert.True(d.UnknownProfitAmount >= 77m);

        var s = (await client.GetFromJsonAsync<IntegrationTestHelpers.SummaryDto>(
            "/api/reports/summary?period=today"))!;
        Assert.True(s.SalesTotal >= 77m);
        Assert.True(s.CashSales >= 77m);                     // cash split includes the manual sale
        Assert.True(s.UnknownProfitSalesCount >= 1);
        Assert.True(s.UnknownProfitAmount >= 77m);
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
            paymentType,
            customerId = (Guid?)null
        });

    private static Task SellOnCreditAsync(HttpClient client, Guid productId, int quantity, Guid customerId) =>
        client.PostAsJsonAsync("/api/sales", new
        {
            productId,
            quantity,
            salePrice = 10m,
            paymentType = "Nisyə",
            customerId = (Guid?)customerId
        });

    private static Task AddGeneralExpenseAsync(HttpClient client, decimal amount) =>
        client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Hesabat xərci",
            category = "Mağaza xərci",
            source = "general",
            amount,
            date = (DateTime?)null,
            productId = (Guid?)null,
            note = (string?)null
        });

    private static Task AddProductExpenseAsync(HttpClient client, Guid productId, decimal amount) =>
        client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Karqo",
            category = "Yol pulu",
            source = "product",
            amount,
            date = (DateTime?)null,
            productId,
            note = (string?)null
        });

    [Fact]
    public async Task Summary_Splits_Expenses_By_Source_And_Sums_To_The_Total()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("RPT-SRC-SUM", quantity: 10, salePrice: 10m);

        await AddGeneralExpenseAsync(client, amount: 100m);
        await AddProductExpenseAsync(client, product.Id, amount: 250m);

        var s = (await client.GetFromJsonAsync<IntegrationTestHelpers.SummaryDto>(
            "/api/reports/summary?period=today"))!;

        Assert.True(s.GeneralExpenses >= 100m);
        Assert.True(s.ProductExpenses >= 250m);
        // BE#33: the split now has three parts (general/product/salary) but must still sum to the total.
        Assert.Equal(s.Expenses, s.GeneralExpenses + s.ProductExpenses + s.SalaryExpenses);
        Assert.Equal(s.Profit - s.Expenses, s.NetProfit);                // net profit formula unaffected
    }

    /// <summary>
    /// BE#33: today's salary PAYMENT (BE#28 <c>salary-entries</c>, <c>type=payment</c>) must reach
    /// <c>SummaryDto.SalaryExpenses</c> and be folded into <c>Expenses</c>/<c>NetProfit</c> exactly like the
    /// day-end close already does — this is the pre-close preview the bug report is about. A same-day
    /// DEDUCTION must never reach any of these figures (charged to the employee's account only).
    /// </summary>
    [Fact]
    public async Task Summary_Today_Includes_Salary_Payments_But_Excludes_Deductions()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var before = (await client.GetFromJsonAsync<IntegrationTestHelpers.SummaryDto>(
            "/api/reports/summary?period=today"))!;

        await AddSalaryEntryAsync(client, type: "payment", amount: 120m);
        await AddSalaryEntryAsync(client, type: "deduction", amount: 15m);

        var after = (await client.GetFromJsonAsync<IntegrationTestHelpers.SummaryDto>(
            "/api/reports/summary?period=today"))!;

        Assert.Equal(120m, after.SalaryExpenses - before.SalaryExpenses);   // only the payment counted
        Assert.Equal(120m, after.Expenses - before.Expenses);               // …and folded into the total
        Assert.Equal(after.Profit - after.Expenses, after.NetProfit);       // net profit formula unaffected
        Assert.Equal(after.Expenses, after.GeneralExpenses + after.ProductExpenses + after.SalaryExpenses);
    }

    /// <summary>Records a salary line for the seeded employee no other reports test depends on.</summary>
    private static async Task AddSalaryEntryAsync(HttpClient client, string type, decimal amount)
    {
        List<IntegrationTestHelpers.EmployeeDto> employees =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.EmployeeDto>>("/api/employees"))!;
        Guid employeeId = employees.Single(e => e.Phone == IntegrationTestHelpers.SecondSellerPhone).Id;

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/employees/{employeeId}/salary-entries",
            new { type, amount, note = (string?)null, month = (string?)null });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// BE#19 at the HTTP boundary: <c>GET /api/reports/summary</c>'s Nağd/Kart/Nisyə must be the money
    /// actually received, exactly like the dashboard and the day-end close. The bug's repro — Total 500 with
    /// 300 received in cash — used to report Cash 0 / Nisyə 500 here while the dashboard said Cash 300 /
    /// Nisyə 200. Asserted as deltas around the one sale, so the shared, accumulating database cannot
    /// weaken the check into a lower bound.
    /// </summary>
    [Fact]
    public async Task Summary_Splits_A_Partially_Paid_Credit_Sale_Into_Received_Cash_And_Remaining_Debt()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("RPT-BE19", quantity: 50, salePrice: 10m);
        var customer = await client.CreateCustomerAsync("Qismən ödəyən müştəri");

        var before = (await client.GetFromJsonAsync<IntegrationTestHelpers.SummaryDto>(
            "/api/reports/summary?period=today"))!;

        HttpResponseMessage sale = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 50,
            salePrice = 10m,
            paymentType = "Nisyə",
            customerId = (Guid?)customer.Id,
            paidAmount = 300m,
            paidVia = "Nağd"
        });
        sale.EnsureSuccessStatusCode();

        var after = (await client.GetFromJsonAsync<IntegrationTestHelpers.SummaryDto>(
            "/api/reports/summary?period=today"))!;

        decimal cash = after.CashSales - before.CashSales;
        decimal card = after.CardSales - before.CardSales;
        decimal credit = after.CreditSales - before.CreditSales;

        Assert.Equal(300m, cash);                              // the down-payment is real cash income…
        Assert.Equal(200m, credit);                            // …and only the remainder is still owed
        Assert.Equal(0m, card);
        Assert.Equal(500m, after.SalesTotal - before.SalesTotal);
        // The split reconciles: every manat of the sale is either received or owed, never both.
        Assert.Equal(after.SalesTotal - before.SalesTotal, cash + card + credit);

        // And the dashboard — the other side of the BE#19 mismatch — agrees on the cash figure.
        var d = (await client.GetFromJsonAsync<IntegrationTestHelpers.DashboardDto>("/api/reports/dashboard"))!;
        Assert.True(d.TodaySales >= 500m);
    }

    // ---------------------------------------------------------------------------------------------
    // BE#27 — page KPI endpoints (products-kpi / sales-kpi / debts-kpi). The shared database
    // accumulates across the whole run, so every test asserts a delta around its own action (before
    // vs. after) rather than an absolute figure — the same pattern the BE#19 test above uses.
    // ---------------------------------------------------------------------------------------------

    private static Task<IntegrationTestHelpers.ProductsKpiDto> ProductsKpiAsync(HttpClient client) =>
        client.GetFromJsonAsync<IntegrationTestHelpers.ProductsKpiDto>("/api/reports/products-kpi")!;

    private static Task<IntegrationTestHelpers.SalesKpiDto> SalesKpiAsync(HttpClient client) =>
        client.GetFromJsonAsync<IntegrationTestHelpers.SalesKpiDto>("/api/reports/sales-kpi")!;

    private static Task<IntegrationTestHelpers.DebtsKpiDto> DebtsKpiAsync(HttpClient client) =>
        client.GetFromJsonAsync<IntegrationTestHelpers.DebtsKpiDto>("/api/reports/debts-kpi")!;

    [Fact]
    public async Task ProductsKpi_PK_I1_Reflects_New_Product_Sale_And_Positive_Adjustment()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var before = await ProductsKpiAsync(client);

        // New product: opening stock 10 (real cost 5, sale price 10) → +10 stock units, +50 cost, +100 sale value.
        var product = await client.CreateProductAsync("PK-I1", quantity: 10, salePrice: 10m, purchasePrice: 5m);
        await SellAsync(client, product.Id, quantity: 3, "Nağd");          // -3 stock, +3 soldUnits
        HttpResponseMessage adjust = await client.AdjustStockAsync(product.Id, delta: 5);  // +5 purchasedUnits
        adjust.EnsureSuccessStatusCode();

        var after = await ProductsKpiAsync(client);

        Assert.Equal(1, after.ProductCount - before.ProductCount);
        Assert.Equal(12, after.TotalStockUnits - before.TotalStockUnits);   // 10 - 3 + 5
        Assert.Equal(3, after.SoldUnits - before.SoldUnits);
        Assert.Equal(15, after.PurchasedUnits - before.PurchasedUnits);     // 10 (opening) + 5 (adjustment)
    }

    [Fact]
    public async Task ProductsKpi_PK_I2_Explicit_Range_Excludes_Movements_Outside_It_But_Not_Snapshot_Fields()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("PK-I2", quantity: 10, salePrice: 10m, purchasePrice: 5m);
        await SellAsync(client, product.Id, quantity: 2, "Nağd");

        var beforeSnapshot = await ProductsKpiAsync(client); // "as of now" snapshot fields, unbounded call

        // A range strictly in the future (relative to this run) sees none of the movements above.
        DateOnly future = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);
        var future1 = await client.GetFromJsonAsync<IntegrationTestHelpers.ProductsKpiDto>(
            $"/api/reports/products-kpi?from={future:yyyy-MM-dd}&to={future:yyyy-MM-dd}");

        Assert.NotNull(future1);
        Assert.Equal(0, future1!.SoldUnits);
        Assert.Equal(0, future1.PurchasedUnits);
        // Snapshot fields never move with from/to — same "as of now" totals as the unbounded call.
        Assert.Equal(beforeSnapshot.ProductCount, future1.ProductCount);
        Assert.Equal(beforeSnapshot.TotalStockUnits, future1.TotalStockUnits);
    }

    [Fact]
    public async Task ProductsKpi_PK_I3_Reversed_Range_Returns_400()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage resp = await client.GetAsync("/api/reports/products-kpi?from=2026-08-10&to=2026-08-01");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var error = (await resp.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Reports.InvalidDateRange", error.Code);
    }

    [Fact]
    public async Task ProductsKpi_PK_I4_Without_Auth_Returns_401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage resp = await client.GetAsync("/api/reports/products-kpi");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    /// <summary>
    /// BE#31 (AC-G4): an unparsable <c>from</c> must come back as a normal { code, message } 400, not a
    /// 500 raised by minimal-API's DateOnly? model binding — see BE#31 repro (from=not-a-date).
    /// </summary>
    [Fact]
    public async Task ProductsKpi_PK_I5_Unparsable_From_Returns_400_Not_500()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage resp =
            await client.GetAsync("/api/reports/products-kpi?from=not-a-date&to=2026-08-02");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var error = (await resp.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Reports.InvalidFrom", error.Code);
    }

    [Fact]
    public async Task SalesKpi_SK_I1_Reflects_A_Cash_Sale_In_Revenue_Profit_And_ByPayment()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var before = await SalesKpiAsync(client);

        // SellAsync always sells at 10m/unit regardless of the product's own sale price (see its body
        // below); with the default 5m purchase price/real cost, one unit is total 10, profit 5.
        var product = await client.CreateProductAsync("SK-I1", quantity: 10);
        await SellAsync(client, product.Id, quantity: 1, "Nağd");

        var after = await SalesKpiAsync(client);

        Assert.Equal(1, after.SalesCount - before.SalesCount);
        Assert.Equal(10m, after.TotalRevenue - before.TotalRevenue);
        Assert.Equal(5m, after.TotalProfit - before.TotalProfit);

        decimal cashRevenueBefore = before.ByPayment.Single(p => p.Type == "Nağd").Revenue;
        decimal cashRevenueAfter = after.ByPayment.Single(p => p.Type == "Nağd").Revenue;
        Assert.Equal(10m, cashRevenueAfter - cashRevenueBefore);
    }

    [Fact]
    public async Task SalesKpi_SK_I2_Reversed_Range_Returns_400()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage resp = await client.GetAsync("/api/reports/sales-kpi?from=2026-08-10&to=2026-08-01");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var error = (await resp.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Reports.InvalidDateRange", error.Code);
    }

    [Fact]
    public async Task SalesKpi_SK_I3_Without_Auth_Returns_401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage resp = await client.GetAsync("/api/reports/sales-kpi");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    /// <summary>BE#31 (AC-G4): same unparsable-date protection as products-kpi, for sales-kpi.</summary>
    [Fact]
    public async Task SalesKpi_SK_I4_Unparsable_From_Returns_400_Not_500()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage resp = await client.GetAsync("/api/reports/sales-kpi?from=not-a-date&to=2026-08-02");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var error = (await resp.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Reports.InvalidFrom", error.Code);
    }

    [Fact]
    public async Task DebtsKpi_DK_I1_Reflects_A_Credit_Sale_And_A_Payment()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var before = await DebtsKpiAsync(client);

        var product = await client.CreateProductAsync("DK-I1", quantity: 10, salePrice: 100m);
        var customer = await client.CreateCustomerAsync("BE27 borclu");

        HttpResponseMessage sale = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 1,
            salePrice = 100m,
            paymentType = "Nisyə",
            customerId = (Guid?)customer.Id
        });
        sale.EnsureSuccessStatusCode(); // +100 debt

        HttpResponseMessage payment = await client.PostAsJsonAsync(
            $"/api/customers/{customer.Id}/payments", new { amount = 40m, note = (string?)null });
        payment.EnsureSuccessStatusCode();

        var after = await DebtsKpiAsync(client);

        Assert.Equal(60m, after.TotalOutstanding - before.TotalOutstanding);   // 100 - 40
        Assert.True(after.DebtorCount >= before.DebtorCount);
        Assert.Equal(100m, after.PeriodNewDebt - before.PeriodNewDebt);
        Assert.Equal(40m, after.PeriodCollected - before.PeriodCollected);
        Assert.NotNull(after.TopDebtor);
    }

    [Fact]
    public async Task DebtsKpi_DK_I3_Reversed_Range_Returns_400()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage resp = await client.GetAsync("/api/reports/debts-kpi?from=2026-08-10&to=2026-08-01");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var error = (await resp.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Reports.InvalidDateRange", error.Code);
    }

    [Fact]
    public async Task DebtsKpi_DK_I4_Without_Auth_Returns_401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage resp = await client.GetAsync("/api/reports/debts-kpi");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    /// <summary>BE#31 (AC-G4): same unparsable-date protection as products-kpi, for debts-kpi.</summary>
    [Fact]
    public async Task DebtsKpi_DK_I5_Unparsable_From_Returns_400_Not_500()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage resp = await client.GetAsync("/api/reports/debts-kpi?from=not-a-date&to=2026-08-02");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var error = (await resp.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Reports.InvalidFrom", error.Code);
    }
}
