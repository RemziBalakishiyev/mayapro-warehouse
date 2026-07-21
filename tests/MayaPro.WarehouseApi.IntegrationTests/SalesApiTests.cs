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

        var page = (await client.GetFromJsonAsync<IntegrationTestHelpers.PagedSalesDto>("/api/sales"))!;
        Assert.Contains(page.Items, s => s.Id == sale.Id);
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
        var page = (await client.GetFromJsonAsync<IntegrationTestHelpers.PagedSalesDto>("/api/sales"))!;
        Assert.Contains(page.Items, s => s.Id == sale.Id);
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

    [Fact]
    public async Task Manual_Sale_Persists_Expense_Items_And_Detail_Returns_Them()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = (Guid?)null,
            productName = "Əl ilə mal (xərcli)",
            quantity = 2,
            salePrice = 20m,
            discount = 0m,
            costPerUnit = 12m,             // frontend-supplied; expense lines are documentation only
            paymentType = "Nağd",
            customerId = (Guid?)null,
            expenseItems = new[]
            {
                new { name = "Yol pulu", amount = 5m },
                new { name = "Fəhlə", amount = 3m }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;

        var detail = (await client.GetFromJsonAsync<IntegrationTestHelpers.SaleDetailDto>(
            $"/api/sales/{created.Id}"))!;

        Assert.True(detail.IsManual);
        Assert.Equal(2, detail.ExpenseItems.Count);
        Assert.Contains(detail.ExpenseItems, e => e.Name == "Yol pulu" && e.Amount == 5m);
        Assert.Contains(detail.ExpenseItems, e => e.Name == "Fəhlə" && e.Amount == 3m);
    }

    [Fact]
    public async Task GetSaleById_Credit_Sale_Populates_CustomerName_And_Current_Product_Name()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("SALE-DETAIL", quantity: 10, salePrice: 20m);
        var customer = await client.CreateCustomerAsync("Detal müştəri", debt: 0m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 1,
            salePrice = 20m,
            discount = 0m,
            paymentType = "Nisyə",
            customerId = customer.Id
        });
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;

        var detail = (await client.GetFromJsonAsync<IntegrationTestHelpers.SaleDetailDto>(
            $"/api/sales/{created.Id}"))!;

        Assert.Equal(customer.Id, detail.CustomerId);
        Assert.Equal("Detal müştəri", detail.CustomerName);
        // The snapshot name and the product's current catalogue name both resolve to the same value here.
        Assert.Equal(product.Name, detail.CurrentProductName);
        Assert.Empty(detail.ExpenseItems);   // catalogued sale carries no free-form expense lines
    }

    [Fact]
    public async Task GetSaleById_Nonexistent_Returns_404()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.GetAsync($"/api/sales/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Sales.NotFound", error.Code);
    }

    [Fact]
    public async Task GetSales_Pages_And_Filters_By_From_To_Range()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        // Shared test DB is reset once per run — measure baseline so earlier tests' sales don't break counts.
        int baseline = (await client.GetFromJsonAsync<IntegrationTestHelpers.PagedSalesDto>(
            "/api/sales?take=1"))!.Total;

        // Free-form sales avoid stock setup; 60 rows so take=50 must leave a second page.
        for (int i = 0; i < 60; i++)
        {
            HttpResponseMessage created = await client.PostAsJsonAsync("/api/sales", new
            {
                productId = (Guid?)null,
                productName = $"Səhifə malı {i}",
                quantity = 1,
                salePrice = 1m,
                discount = 0m,
                paymentType = "Nağd",
                customerId = (Guid?)null
            });
            created.EnsureSuccessStatusCode();
        }

        int expectedTotal = baseline + 60;

        var firstPage = (await client.GetFromJsonAsync<IntegrationTestHelpers.PagedSalesDto>(
            "/api/sales?take=50&skip=0"))!;
        Assert.Equal(expectedTotal, firstPage.Total);
        Assert.Equal(50, firstPage.Items.Count);
        Assert.Equal(0, firstPage.Skip);
        Assert.Equal(50, firstPage.Take);

        var secondPage = (await client.GetFromJsonAsync<IntegrationTestHelpers.PagedSalesDto>(
            "/api/sales?take=50&skip=50"))!;
        Assert.Equal(expectedTotal, secondPage.Total);
        Assert.Equal(expectedTotal - 50, secondPage.Items.Count);

        // All new sales fall on today (Baku, UTC+4) — from/to for today keeps them; a distant past window is empty.
        string today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(4)).ToString("yyyy-MM-dd");
        var todayRange = (await client.GetFromJsonAsync<IntegrationTestHelpers.PagedSalesDto>(
            $"/api/sales?from={today}&to={today}&take=200"))!;
        Assert.Equal(expectedTotal, todayRange.Total);
        Assert.Equal(expectedTotal, todayRange.Items.Count);

        var emptyRange = (await client.GetFromJsonAsync<IntegrationTestHelpers.PagedSalesDto>(
            "/api/sales?from=2020-01-01&to=2020-01-02&take=50"))!;
        Assert.Equal(0, emptyRange.Total);
        Assert.Empty(emptyRange.Items);
    }

    [Fact]
    public async Task Delete_Nonexistent_Sale_Returns_404()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.DeleteAsync($"/api/sales/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Sales.NotFound", error.Code);
    }

    [Fact]
    public async Task Delete_Cash_Sale_Returns_Its_Stock()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("SALE-DEL-STOCK", quantity: 20, salePrice: 10m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = product.Id, quantity = 3, salePrice = 10m, discount = 0m,
            paymentType = "Nağd", customerId = (Guid?)null
        });
        Assert.Equal(17, (await client.GetProductAsync(product.Id)).Quantity);

        HttpResponseMessage delete = await client.DeleteAsync($"/api/sales/{sale.Id}");

        // The shared day may already be closed by the day-end tests — assert the right outcome either way.
        var afterQty = (await client.GetProductAsync(product.Id)).Quantity;
        if (delete.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal(20, afterQty); // reserved stock returned
        }
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
            var error = (await delete.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
            Assert.Equal("Sales.DayClosedConflict", error.Code);
            Assert.Equal(17, afterQty); // guard held — nothing changed
        }
    }

    [Fact]
    public async Task Delete_Credit_Sale_Reduces_Customer_Debt()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("SALE-DEL-DEBT", quantity: 10, salePrice: 20m);
        var customer = await client.CreateCustomerAsync("Nisyə, silinən satış", debt: 0m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = product.Id, quantity = 2, salePrice = 20m, discount = 5m,
            paymentType = "Nisyə", customerId = customer.Id
        });
        Assert.Equal(35m, (await client.GetCustomerAsync(customer.Id)).Debt); // 20*2 - 5

        HttpResponseMessage delete = await client.DeleteAsync($"/api/sales/{sale.Id}");

        decimal afterDebt = (await client.GetCustomerAsync(customer.Id)).Debt;
        if (delete.StatusCode == HttpStatusCode.OK)
            Assert.Equal(0m, afterDebt); // the credit was unwound
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
            Assert.Equal(35m, afterDebt); // guard held
        }
    }

    [Fact]
    public async Task Update_Sale_Applies_The_Stock_Difference()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("SALE-UPD-STOCK", quantity: 20, salePrice: 10m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = product.Id, quantity = 3, salePrice = 10m, discount = 0m,
            paymentType = "Nağd", customerId = (Guid?)null
        });
        Assert.Equal(17, (await client.GetProductAsync(product.Id)).Quantity);

        // Raise the quantity 3 → 5: reverse (+3) then reapply (−5) nets one more unit off stock.
        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/sales/{sale.Id}", new
        {
            productId = product.Id, quantity = 5, salePrice = 10m, discount = 0m,
            paymentType = "Nağd", customerId = (Guid?)null
        });

        var afterQty = (await client.GetProductAsync(product.Id)).Quantity;
        if (update.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal(15, afterQty); // 20 − 5
            var detail = (await client.GetFromJsonAsync<IntegrationTestHelpers.SaleDetailDto>(
                $"/api/sales/{sale.Id}"))!;
            Assert.Equal(5, detail.Quantity);
            Assert.Equal(50m, detail.TotalAmount);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
            var error = (await update.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
            Assert.Equal("Sales.DayClosedConflict", error.Code);
            Assert.Equal(17, afterQty); // guard held
        }
    }

    [Fact]
    public async Task Update_Sale_Beyond_Available_Stock_Rolls_Back()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("SALE-UPD-OVER", quantity: 5, salePrice: 10m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = product.Id, quantity = 2, salePrice = 10m, discount = 0m,
            paymentType = "Nağd", customerId = (Guid?)null
        });
        Assert.Equal(3, (await client.GetProductAsync(product.Id)).Quantity);

        // Reversing returns 2 (→5 available), so 6 is still one beyond what exists → fail + rollback.
        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/sales/{sale.Id}", new
        {
            productId = product.Id, quantity = 6, salePrice = 10m, discount = 0m,
            paymentType = "Nağd", customerId = (Guid?)null
        });

        // Either a closed day (409) or the stock shortfall (400) — in both cases nothing changes.
        Assert.NotEqual(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(3, (await client.GetProductAsync(product.Id)).Quantity); // rollback / guard
        var still = (await client.GetFromJsonAsync<IntegrationTestHelpers.SaleDetailDto>(
            $"/api/sales/{sale.Id}"))!;
        Assert.Equal(2, still.Quantity); // the sale kept its original quantity
    }

    private static async Task<IntegrationTestHelpers.SaleDto> CreateSaleAsync(HttpClient client, object body)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;
    }
}
