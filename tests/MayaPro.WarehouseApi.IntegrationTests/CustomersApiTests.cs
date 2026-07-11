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
}
