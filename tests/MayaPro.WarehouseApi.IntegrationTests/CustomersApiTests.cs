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
            paymentType = "Nisyə",
            customerId = customer.Id
        });
        sale.EnsureSuccessStatusCode();
        var createdSale = (await sale.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;

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
        Assert.Null(history[0].SaleId);

        Assert.Equal("sale", history[1].Type);
        Assert.Equal(30m, history[1].Amount);
        Assert.Contains(product.Name, history[1].Note);
        Assert.Equal(createdSale.Id, history[1].SaleId);

        Assert.Equal("payment", history[2].Type);
        Assert.Equal(40m, history[2].Amount);
        Assert.Equal("Hissəvi ödəniş", history[2].Note);
        Assert.Null(history[2].SaleId);

        // And the timestamps are actually non-decreasing.
        Assert.True(history[0].Date <= history[1].Date);
        Assert.True(history[1].Date <= history[2].Date);
    }

    [Fact]
    public async Task Delete_Customer_Credit_From_History_Unwinds_Debt()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("HIST-DEL", quantity: 10, salePrice: 15m);
        var customer = await client.CreateCustomerAsync("Nisyə, borc silinir", debt: 0m);

        HttpResponseMessage sale = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 2,
            salePrice = 15m,
            paymentType = "Nisyə",
            customerId = customer.Id
        });
        sale.EnsureSuccessStatusCode();
        var createdSale = (await sale.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;

        List<IntegrationTestHelpers.CustomerHistoryEntryDto> history =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.CustomerHistoryEntryDto>>(
                $"/api/customers/{customer.Id}/history"))!;
        Guid saleId = history.Single(h => h.Type == "sale").SaleId!.Value;
        Assert.Equal(createdSale.Id, saleId);

        HttpResponseMessage delete = await client.DeleteAsync(
            $"/api/customers/{customer.Id}/credits/{saleId}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(0m, (await client.GetCustomerAsync(customer.Id)).Debt);
        history = (await client.GetFromJsonAsync<List<IntegrationTestHelpers.CustomerHistoryEntryDto>>(
            $"/api/customers/{customer.Id}/history"))!;
        Assert.DoesNotContain(history, h => h.Type == "sale");
    }

    [Fact]
    public async Task Delete_Customer_Credit_With_Partial_Payment_Still_Succeeds()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("HIST-DEL-PART", quantity: 10, salePrice: 20m);
        var customer = await client.CreateCustomerAsync("Qismən ödənilmiş nisyə", debt: 0m);

        HttpResponseMessage sale = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 2,
            salePrice = 20m,
            paymentType = "Nisyə",
            customerId = customer.Id
        });
        sale.EnsureSuccessStatusCode();
        var createdSale = (await sale.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;

        HttpResponseMessage payment = await client.PostAsJsonAsync(
            $"/api/customers/{customer.Id}/payments", new { amount = 20m, note = "Hissəvi ödəniş" });
        payment.EnsureSuccessStatusCode();
        Assert.Equal(createdSale.TotalAmount - 20m, (await client.GetCustomerAsync(customer.Id)).Debt);

        HttpResponseMessage delete = await client.DeleteAsync(
            $"/api/customers/{customer.Id}/credits/{createdSale.Id}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(0m, (await client.GetCustomerAsync(customer.Id)).Debt);
    }

    [Fact]
    public async Task Update_Customer_Changes_Details_And_Leaves_Debt_Untouched()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var customer = await client.CreateCustomerAsync("Köhnə ad", debt: 40m);

        HttpResponseMessage update = await client.PutAsJsonAsync(
            $"/api/customers/{customer.Id}", new { name = "Yeni ad", phone = "0559998877", note = "VIP" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var after = await client.GetCustomerAsync(customer.Id);
        Assert.Equal("Yeni ad", after.Name);
        Assert.Equal(40m, after.Debt); // an edit never moves the balance
    }

    [Fact]
    public async Task Delete_Customer_With_Debt_Removes_The_Customer()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var customer = await client.CreateCustomerAsync("Borclu, silinən", debt: 75m);

        HttpResponseMessage delete = await client.DeleteAsync($"/api/customers/{customer.Id}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        List<IntegrationTestHelpers.CustomerDto> all =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.CustomerDto>>("/api/customers"))!;
        Assert.DoesNotContain(all, c => c.Id == customer.Id);
    }

    [Fact]
    public async Task Delete_Debt_Free_Customer_Removes_The_Customer()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        // Opening debt then paid off to zero — leaves an adjustment + payment history to be removed with them.
        var customer = await client.CreateCustomerAsync("Ödənilmiş, silinən", debt: 50m);
        HttpResponseMessage pay = await client.PostAsJsonAsync(
            $"/api/customers/{customer.Id}/payments", new { amount = 50m, note = (string?)null });
        pay.EnsureSuccessStatusCode();

        HttpResponseMessage delete = await client.DeleteAsync($"/api/customers/{customer.Id}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        List<IntegrationTestHelpers.CustomerDto> all =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.CustomerDto>>("/api/customers"))!;
        Assert.DoesNotContain(all, c => c.Id == customer.Id);
    }

    [Fact]
    public async Task Seller_Cannot_Delete_Customer_Returns_403()
    {
        HttpClient owner = await _factory.AuthenticatedClientAsync();
        var customer = await owner.CreateCustomerAsync("Satıcı silə bilməz", debt: 0m);

        HttpClient seller = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.SellerPhone);
        HttpResponseMessage delete = await seller.DeleteAsync($"/api/customers/{customer.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Cash_Sale_With_Customer_Leaves_Debt_But_Counts_In_Stats_And_History()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var customer = await client.CreateCustomerAsync("Nağd alıcı", debt: 25m);
        var product = await client.CreateProductAsync("CUST-CASH-1", quantity: 10, salePrice: 15m);

        HttpResponseMessage sale = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 2,
            salePrice = 15m,
            paymentType = "Nağd",
            customerId = customer.Id
        });
        sale.EnsureSuccessStatusCode();
        var createdSale = (await sale.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;

        // Debt is untouched — only credit sales raise it.
        var reread = await client.GetCustomerAsync(customer.Id);
        Assert.Equal(25m, reread.Debt);

        // But the purchase stats cover every payment type.
        Assert.Equal(30m, reread.TotalPurchases);
        Assert.Equal(1, reread.PurchaseCount);
        Assert.NotNull(reread.LastPurchaseDate);

        // And the sale shows in the history, flagged with its payment type.
        List<IntegrationTestHelpers.CustomerHistoryEntryDto> history =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.CustomerHistoryEntryDto>>(
                $"/api/customers/{customer.Id}/history"))!;
        var saleEntry = Assert.Single(history, h => h.Type == "sale");
        Assert.Equal(createdSale.Id, saleEntry.SaleId);
        Assert.Equal("Nağd", saleEntry.PaymentType);
        Assert.Equal(30m, saleEntry.Amount);
    }

    [Fact]
    public async Task Credit_Sale_Still_Requires_Customer()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("CUST-CRED-REQ", quantity: 5, salePrice: 10m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 1,
            salePrice = 10m,
            paymentType = "Nisyə",
            customerId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Nisyə satış üçün müştəri seçilməlidir", error.Message);
    }
}
