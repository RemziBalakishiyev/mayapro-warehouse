using System.Net;
using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for the debt chain: a credit sale builds debt, a payment reduces it, and an
/// overpayment is rejected without changing the balance.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CustomersApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public CustomersApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Payment_After_Credit_Sale_Reduces_Debt()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("PAY-CHAIN", quantity: 10, salePrice: 10m);
        var customer = await client.CreateCustomerAsync("Borclu müştəri", debt: 0m);

        // Credit sale → debt becomes 30.
        await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 3,
            salePrice = 10m,
            discount = 0m,
            paymentType = "Nisyə",
            customerId = customer.Id
        });

        HttpResponseMessage payment = await client.PostAsJsonAsync(
            $"/api/customers/{customer.Id}/payments", new { amount = 20m, note = "İlk ödəniş" });

        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);

        var afterPayment = await client.GetCustomerAsync(customer.Id);
        Assert.Equal(10m, afterPayment.Debt);

        List<IntegrationTestHelpers.CustomerPaymentDto> payments =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.CustomerPaymentDto>>(
                $"/api/customers/{customer.Id}/payments"))!;
        Assert.Single(payments);
        Assert.Equal(20m, payments[0].Amount);
    }

    [Fact]
    public async Task Payment_Exceeding_Debt_Returns_400_And_Leaves_Debt_Untouched()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var customer = await client.CreateCustomerAsync("Az borclu", debt: 30m);

        HttpResponseMessage payment = await client.PostAsJsonAsync(
            $"/api/customers/{customer.Id}/payments", new { amount = 50m, note = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, payment.StatusCode);
        var error = (await payment.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Customers.PaymentExceedsDebt", error.Code);

        var afterAttempt = await client.GetCustomerAsync(customer.Id);
        Assert.Equal(30m, afterAttempt.Debt);
    }

    [Fact]
    public async Task Customer_Created_With_Initial_Debt_Sets_Debt_And_Records_History_Row()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var customer = await client.CreateCustomerAsync("İlkin borclu müştəri", debt: 150m);

        // The opening debt lands on both the debt balance and the dedicated InitialDebt field.
        Assert.Equal(150m, customer.Debt);
        Assert.Equal(150m, customer.InitialDebt);

        List<IntegrationTestHelpers.CustomerHistoryEntryDto> history =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.CustomerHistoryEntryDto>>(
                $"/api/customers/{customer.Id}/history"))!;

        var initial = Assert.Single(history);
        Assert.Equal("initialDebt", initial.Type);
        Assert.Equal(150m, initial.Amount);
        Assert.Equal("İlkin borc (sistemə keçid)", initial.Note);
    }

    [Fact]
    public async Task Customer_Created_Without_Debt_Has_Empty_History()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var customer = await client.CreateCustomerAsync("Borcsuz müştəri", debt: 0m);

        Assert.Equal(0m, customer.Debt);
        Assert.Equal(0m, customer.InitialDebt);

        List<IntegrationTestHelpers.CustomerHistoryEntryDto> history =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.CustomerHistoryEntryDto>>(
                $"/api/customers/{customer.Id}/history"))!;

        Assert.Empty(history);
    }

    [Fact]
    public async Task History_Returns_Initial_Debt_Sale_And_Payment_In_Chronological_Order()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("HIST-CHAIN", quantity: 10, salePrice: 10m);
        var customer = await client.CreateCustomerAsync("Tam tarixçə", debt: 100m);

        // Credit sale → adds 30 debt.
        HttpResponseMessage sale = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 3,
            salePrice = 10m,
            discount = 0m,
            paymentType = "Nisyə",
            customerId = customer.Id
        });
        sale.EnsureSuccessStatusCode();

        // Payment → reduces debt by 40.
        HttpResponseMessage payment = await client.PostAsJsonAsync(
            $"/api/customers/{customer.Id}/payments", new { amount = 40m, note = "Hissəvi ödəniş" });
        payment.EnsureSuccessStatusCode();

        List<IntegrationTestHelpers.CustomerHistoryEntryDto> history =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.CustomerHistoryEntryDto>>(
                $"/api/customers/{customer.Id}/history"))!;

        Assert.Equal(3, history.Count);

        // Chronological: opening balance, then the credit sale, then the payment.
        Assert.Equal("initialDebt", history[0].Type);
        Assert.Equal(100m, history[0].Amount);

        Assert.Equal("sale", history[1].Type);
        Assert.Equal(30m, history[1].Amount);
        Assert.Contains(product.Name, history[1].Note);

        Assert.Equal("payment", history[2].Type);
        Assert.Equal(40m, history[2].Amount);
        Assert.Equal("Hissəvi ödəniş", history[2].Note);

        // And the timestamps are actually non-decreasing.
        Assert.True(history[0].Date <= history[1].Date);
        Assert.True(history[1].Date <= history[2].Date);
    }
}
