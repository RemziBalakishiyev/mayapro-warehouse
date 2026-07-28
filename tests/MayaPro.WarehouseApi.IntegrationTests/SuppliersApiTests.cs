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
    public async Task Supplier_ItemCount_Reflects_Linked_Products()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var supplier = await client.CreateSupplierAsync("Mallı təchizatçı");

        // A freshly created supplier has no products yet.
        var before = await client.GetSupplierAsync(supplier.Id);
        Assert.Equal(0, before.ItemCount);

        // Link a product to this supplier by its id, then the count reflects it.
        await client.CreateProductAsync("SUP-ITEMCOUNT", quantity: 5, supplierId: supplier.Id.ToString());

        var after = await client.GetSupplierAsync(supplier.Id);
        Assert.True(after.ItemCount >= 1);
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

    [Fact]
    public async Task Update_Supplier_Changes_Details_And_Leaves_Debt_Untouched()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var supplier = await client.CreateSupplierAsync("Köhnə təchizatçı", debt: 120m);

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/suppliers/{supplier.Id}", new
        {
            name = "Yeni təchizatçı",
            contactName = "Vəli",
            phone = "0551234567",
            note = "Etibarlı"
        });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var after = await client.GetSupplierAsync(supplier.Id);
        Assert.Equal("Yeni təchizatçı", after.Name);
        Assert.Equal(120m, after.Debt); // an edit never moves the balance
    }

    [Fact]
    public async Task Delete_Supplier_With_Debt_Returns_409_And_Keeps_The_Supplier()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var supplier = await client.CreateSupplierAsync("Borclu, silinməz", debt: 300m);

        HttpResponseMessage delete = await client.DeleteAsync($"/api/suppliers/{supplier.Id}");

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var error = (await delete.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Suppliers.HasDebtConflict", error.Code);

        List<IntegrationTestHelpers.SupplierDto> all =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.SupplierDto>>("/api/suppliers"))!;
        Assert.Contains(all, s => s.Id == supplier.Id);
    }

    [Fact]
    public async Task Delete_Debt_Free_Supplier_Removes_The_Supplier()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var supplier = await client.CreateSupplierAsync("Borcsuz, silinən", debt: 0m);

        HttpResponseMessage delete = await client.DeleteAsync($"/api/suppliers/{supplier.Id}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        List<IntegrationTestHelpers.SupplierDto> all =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.SupplierDto>>("/api/suppliers"))!;
        Assert.DoesNotContain(all, s => s.Id == supplier.Id);
    }

    [Fact]
    public async Task Seller_Cannot_Delete_Supplier_Returns_403()
    {
        HttpClient owner = await _factory.AuthenticatedClientAsync();
        var supplier = await owner.CreateSupplierAsync("Satıcı silə bilməz", debt: 0m);

        HttpClient seller = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.SellerPhone);
        HttpResponseMessage delete = await seller.DeleteAsync($"/api/suppliers/{supplier.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Supplier_Created_With_Initial_Debt_Sets_Debt_And_Records_History_Row()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var supplier = await client.CreateSupplierAsync("İlkin borclu təchizatçı", debt: 150m);

        Assert.Equal(150m, supplier.Debt);

        List<IntegrationTestHelpers.SupplierHistoryEntryDto> history =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.SupplierHistoryEntryDto>>(
                $"/api/suppliers/{supplier.Id}/history"))!;

        var initial = Assert.Single(history);
        Assert.Equal("initialDebt", initial.Type);
        Assert.Equal(150m, initial.Amount);
        Assert.Equal("İlkin borc (sistemə keçid)", initial.Note);
    }

    [Fact]
    public async Task Supplier_Created_Without_Debt_Has_Empty_History()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var supplier = await client.CreateSupplierAsync("Borcsuz təchizatçı", debt: 0m);

        Assert.Equal(0m, supplier.Debt);

        List<IntegrationTestHelpers.SupplierHistoryEntryDto> history =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.SupplierHistoryEntryDto>>(
                $"/api/suppliers/{supplier.Id}/history"))!;

        Assert.Empty(history);
    }

    [Fact]
    public async Task History_Returns_Initial_Debt_And_Payment_In_Chronological_Order_And_Payments_Endpoint_Stays_Unchanged()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var supplier = await client.CreateSupplierAsync("İlkin borc və ödəniş", debt: 150m);

        HttpResponseMessage payment = await client.PostAsJsonAsync(
            $"/api/suppliers/{supplier.Id}/payments", new { amount = 50m, note = (string?)null });
        payment.EnsureSuccessStatusCode();

        List<IntegrationTestHelpers.SupplierHistoryEntryDto> history =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.SupplierHistoryEntryDto>>(
                $"/api/suppliers/{supplier.Id}/history"))!;

        Assert.Equal(2, history.Count);
        Assert.Equal("initialDebt", history[0].Type);
        Assert.Equal(150m, history[0].Amount);
        Assert.Equal("payment", history[1].Type);
        Assert.Equal(50m, history[1].Amount);
        Assert.True(history[0].Date <= history[1].Date);

        // The pre-existing payments-only endpoint stays exactly as it was — no opening-balance row.
        List<IntegrationTestHelpers.SupplierPaymentDto> payments =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.SupplierPaymentDto>>(
                $"/api/suppliers/{supplier.Id}/payments"))!;
        Assert.Single(payments);
        Assert.Equal(50m, payments[0].Amount);
    }

    [Fact]
    public async Task Negative_Initial_Debt_Returns_400_And_Creates_Nothing()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/suppliers", new
        {
            name = "Mənfi borclu təchizatçı",
            contactName = (string?)null,
            phone = (string?)null,
            note = (string?)null,
            debt = -10m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Borc mənfi ola bilməz", error.Message);

        List<IntegrationTestHelpers.SupplierDto> all =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.SupplierDto>>("/api/suppliers"))!;
        Assert.DoesNotContain(all, s => s.Name == "Mənfi borclu təchizatçı");
    }
}
