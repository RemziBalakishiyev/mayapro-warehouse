using System.Net;
using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for the supplier debt chain: a purchase raises what we owe, a payment lowers it, and
/// an overpayment is rejected without changing the balance.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SuppliersApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public SuppliersApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Adding_Debt_Increases_Supplier_Debt()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var supplier = await client.CreateSupplierAsync("Yeni təchizatçı", debt: 0m);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/suppliers/{supplier.Id}/debts", new { amount = 500m, note = "Mal alışı" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var afterDebt = await client.GetSupplierAsync(supplier.Id);
        Assert.Equal(500m, afterDebt.Debt);
    }

    [Fact]
    public async Task Payment_Reduces_Supplier_Debt()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var supplier = await client.CreateSupplierAsync("Borclu təchizatçı", debt: 800m);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/suppliers/{supplier.Id}/payments", new { amount = 300m, note = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var afterPayment = await client.GetSupplierAsync(supplier.Id);
        Assert.Equal(500m, afterPayment.Debt);

        List<IntegrationTestHelpers.SupplierPaymentDto> payments =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.SupplierPaymentDto>>(
                $"/api/suppliers/{supplier.Id}/payments"))!;
        Assert.Single(payments);
        Assert.Equal(300m, payments[0].Amount);
    }

    [Fact]
    public async Task Payment_Exceeding_Debt_Returns_400_And_Leaves_Debt_Untouched()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var supplier = await client.CreateSupplierAsync("Az borclu təchizatçı", debt: 200m);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/suppliers/{supplier.Id}/payments", new { amount = 500m, note = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Suppliers.PaymentExceedsDebt", error.Code);

        var afterAttempt = await client.GetSupplierAsync(supplier.Id);
        Assert.Equal(200m, afterAttempt.Debt);
    }
}
