using System.Net;
using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for the sales chain: cash and credit sales, the insufficient-stock rollback, and the
/// atomicity proof — when a later step of the chain fails, the earlier stock decrement is rolled back.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SalesApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public SalesApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Cash_Sale_Decreases_Stock_And_Records_Sale()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("SALE-CASH", quantity: 20, salePrice: 10m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 3,
            salePrice = 10m,
            discount = 0m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sale = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;
        Assert.Equal(30m, sale.TotalAmount);
        Assert.Equal(3, sale.Quantity);
        Assert.Null(sale.CustomerId);

        var afterSale = await client.GetProductAsync(product.Id);
        Assert.Equal(17, afterSale.Quantity);

        List<IntegrationTestHelpers.SaleDto> allSales =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.SaleDto>>("/api/sales"))!;
        Assert.Contains(allSales, s => s.Id == sale.Id);
    }

    [Fact]
    public async Task Credit_Sale_Increases_Customer_Debt_By_Net_Amount()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("SALE-CREDIT", quantity: 10, salePrice: 20m);
        var customer = await client.CreateCustomerAsync("Nisyə müştəri", debt: 0m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 2,
            salePrice = 20m,
            discount = 5m,
            paymentType = "Nisyə",
            customerId = customer.Id
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sale = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;
        Assert.Equal(35m, sale.TotalAmount); // 20*2 - 5 discount

        var afterSale = await client.GetCustomerAsync(customer.Id);
        Assert.Equal(35m, afterSale.Debt);
    }

    [Fact]
    public async Task Manual_Cash_Sale_Records_Revenue_And_Touches_No_Stock()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        // A catalogued product exists in the same DB; a free-form sale must leave its stock alone.
        var product = await client.CreateProductAsync("SALE-MANUAL", quantity: 20, salePrice: 10m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = (Guid?)null,           // free-form: no catalogued product
            productName = "Əl ilə mal",
            quantity = 2,
            salePrice = 15m,
            discount = 0m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sale = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;
        Assert.Null(sale.ProductId);
        Assert.True(sale.IsManual);
        Assert.Null(sale.CostPerUnit);   // no cost given → unknown
        Assert.Null(sale.Profit);        // → profit unknown, not zero
        Assert.Equal(30m, sale.TotalAmount);

        // No product's stock moved — the manual sale referenced no catalogue item.
        var afterSale = await client.GetProductAsync(product.Id);
        Assert.Equal(20, afterSale.Quantity);

        // The revenue is still recorded and listed like any other sale.
        List<IntegrationTestHelpers.SaleDto> allSales =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.SaleDto>>("/api/sales"))!;
        Assert.Contains(allSales, s => s.Id == sale.Id);
    }

    [Fact]
    public async Task Manual_Credit_Sale_Increases_Customer_Debt_By_Net_Amount()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var customer = await client.CreateCustomerAsync("Sərbəst nisyə müştəri", debt: 0m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = (Guid?)null,
            productName = "Əl ilə nisyə mal",
            quantity = 3,
            salePrice = 10m,
            discount = 0m,
            paymentType = "Nisyə",
            customerId = customer.Id
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sale = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;
        Assert.True(sale.IsManual);
        Assert.Equal(customer.Id, sale.CustomerId);

        // The credit flow is identical to a catalogued sale — the money owed is just as real.
        var afterSale = await client.GetCustomerAsync(customer.Id);
        Assert.Equal(30m, afterSale.Debt);
    }

    [Fact]
    public async Task Manual_Sale_Without_ProductName_Returns_400()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = (Guid?)null,
            productName = "",   // blank → invalid free-form sale
            quantity = 1,
            salePrice = 10m,
            discount = 0m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Sərbəst satışda mal adı məcburidir", error.Message);
    }

    [Fact]
    public async Task Sale_Beyond_Stock_Returns_400_And_Leaves_Stock_Untouched()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("SALE-OVERSELL", quantity: 5, salePrice: 10m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 10,
            salePrice = 10m,
            discount = 0m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Products.InsufficientStock", error.Code);

        // Rollback proof: stock is exactly what it was.
        var afterSale = await client.GetProductAsync(product.Id);
        Assert.Equal(5, afterSale.Quantity);
    }

    [Fact]
    public async Task Credit_Sale_With_Nonexistent_Customer_Rolls_Back_Stock_Decrement()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("SALE-ATOMIC", quantity: 8, salePrice: 10m);

        // Passes validation (customerId is non-null), but the customer does not exist — the debt step
        // fails AFTER stock was decremented in-memory. The shared transaction must undo the decrement.
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 2,
            salePrice = 10m,
            discount = 0m,
            paymentType = "Nisyə",
            customerId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Customers.NotFound", error.Code);

        // Atomicity proof: the stock decrement was rolled back.
        var afterSale = await client.GetProductAsync(product.Id);
        Assert.Equal(8, afterSale.Quantity);
    }
}
