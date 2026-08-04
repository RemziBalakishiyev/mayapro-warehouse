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
        // BE#15 / AC7: 50 sold, only 30 received in cash → +30 cash and +20 credit, never +50 to either.
        await SellPartiallyPaidAsync(client, product.Id, quantity: 5, paidAmount: 30m, customer.Id);
        await AddGeneralExpenseAsync(client, amount: 40m);                        // +40 expenses

        // BE#28: a salary payment is cash out of the same drawer, so the server must fold it into the day's
        // expense total; a deduction is charged to the employee's account only and must not.
        decimal expensesBeforeSalary = await TodayExpensesAsync(client);
        await AddSalaryEntryAsync(client, type: "payment", amount: 200m);         // +200 expenses
        await AddSalaryEntryAsync(client, type: "deduction", amount: 30m);        // +0 — account only
        decimal expensesWithSalary = await TodayExpensesAsync(client);
        Assert.Equal(expensesBeforeSalary + 200m, expensesWithSalary);            // the 30 deduction is absent

        // BE#33: the pre-close preview (GetSummaryHandler) must already report the same salary payments the
        // actual close is about to fold in — captured here, right before closing, so nothing else can add a
        // salary payment for today in between (this collection runs its tests sequentially).
        var preview = (await client.GetFromJsonAsync<IntegrationTestHelpers.SummaryDto>(
            "/api/reports/summary?period=today"))!;
        Assert.True(preview.SalaryExpenses >= 200m);

        // The client sends only cash figures + note; totals are the server's job.
        HttpResponseMessage close = await client.PostAsJsonAsync(
            "/api/closings", new { openingCash = 100m, actualCash = 90m, note = "Gün sonu" });

        Assert.Equal(HttpStatusCode.Created, close.StatusCode);
        var c = (await close.Content.ReadFromJsonAsync<IntegrationTestHelpers.ClosingDto>())!;

        // Server aggregated real sales/expenses (client sent none of these). The shared integration database
        // carries other tests' sales for the same day, so these are lower bounds — the exact BE#15 arithmetic
        // is pinned down in SalesModuleContractTests.
        Assert.True(c.CashSales >= 60m);     // 30 outright + the partially paid sale's 30 down-payment
        Assert.True(c.CardSales >= 20m);
        Assert.True(c.CreditSales >= 30m);   // 10 owed outright + 20 still owed on the partially paid sale
        Assert.True(c.Expenses >= 240m);     // 40 store expense + the 200 salary payment (BE#28)

        // The closing's expense figure is exactly the day's expenses plus salary payments — the same number
        // the dashboard reports, so the two views of the drawer can never drift apart.
        Assert.Equal(expensesWithSalary, c.Expenses);

        // BE#33: SalaryExpenses is the salary-payments part of Expenses, broken out on its own, and it must
        // agree exactly with what the pre-close preview reported a moment ago — no drift between preview and
        // the actual close.
        Assert.True(c.SalaryExpenses >= 200m);
        Assert.Equal(preview.SalaryExpenses, c.SalaryExpenses);

        // The partially paid sale added its down-payment to the debt-free side and only the rest to the debt.
        Assert.Equal(30m, (await client.GetCustomerAsync(customer.Id)).Debt); // 10 + 20, not 10 + 50

        // Server-side maths is self-consistent.
        Assert.Equal(100m, c.OpeningCash);
        Assert.Equal(c.OpeningCash + c.CashSales - c.Expenses, c.ExpectedCash);
        Assert.Equal(c.ActualCash - c.ExpectedCash, c.Difference);

        // Second close of the same day is rejected.
        HttpResponseMessage second = await client.PostAsJsonAsync(
            "/api/closings", new { openingCash = 100m, actualCash = 90m, note = (string?)null });

        // Re-closing the same day is a conflict (409), not a plain bad request.
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
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
            paymentType,
            customerId
        });

    /// <summary>A Nisyə sale with a cash down-payment (BE#15): paid now, the rest becomes the customer's debt.</summary>
    private static async Task SellPartiallyPaidAsync(
        HttpClient client, Guid productId, int quantity, decimal paidAmount, Guid customerId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId,
            quantity,
            salePrice = 10m,
            paymentType = "Nisyə",
            customerId,
            paidAmount,
            paidVia = "Nağd"
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<decimal> TodayExpensesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<IntegrationTestHelpers.DashboardDto>("/api/reports/dashboard"))!.TodayExpenses;

    /// <summary>Records a salary line for the seeded employee no other day-end figure depends on.</summary>
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

    private static Task AddGeneralExpenseAsync(HttpClient client, decimal amount) =>
        client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Gün sonu xərci",
            category = "Mağaza xərci",
            source = "general",
            amount,
            date = (DateTime?)null,
            productId = (Guid?)null,
            note = (string?)null
        });
}
