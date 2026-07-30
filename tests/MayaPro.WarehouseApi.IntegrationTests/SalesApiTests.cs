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
            paymentType = "Nisyə",
            customerId = customer.Id
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sale = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;
        Assert.Equal(40m, sale.TotalAmount); // 20*2

        var afterSale = await client.GetCustomerAsync(customer.Id);
        Assert.Equal(40m, afterSale.Debt);
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
            productId = product.Id, quantity = 3, salePrice = 10m,
            paymentType = "Nağd", customerId = (Guid?)null
        });
        Assert.Equal(17, (await client.GetProductAsync(product.Id)).Quantity);

        HttpResponseMessage delete = await client.DeleteAsync($"/api/sales/{sale.Id}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(20, (await client.GetProductAsync(product.Id)).Quantity); // reserved stock returned
    }

    [Fact]
    public async Task Delete_Credit_Sale_Reduces_Customer_Debt()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("SALE-DEL-DEBT", quantity: 10, salePrice: 20m);
        var customer = await client.CreateCustomerAsync("Nisyə, silinən satış", debt: 0m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = product.Id, quantity = 2, salePrice = 20m,
            paymentType = "Nisyə", customerId = customer.Id
        });
        Assert.Equal(40m, (await client.GetCustomerAsync(customer.Id)).Debt); // 20*2

        HttpResponseMessage delete = await client.DeleteAsync($"/api/sales/{sale.Id}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(0m, (await client.GetCustomerAsync(customer.Id)).Debt); // the credit was unwound
    }

    [Fact]
    public async Task Delete_Credit_Sale_With_Partial_Payment_Still_Succeeds()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("SALE-DEL-PARTIAL", quantity: 10, salePrice: 20m);
        var customer = await client.CreateCustomerAsync("Qismən ödənilmiş nisyə", debt: 0m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = product.Id, quantity = 2, salePrice = 20m,
            paymentType = "Nisyə", customerId = customer.Id
        });
        Assert.Equal(40m, (await client.GetCustomerAsync(customer.Id)).Debt);

        HttpResponseMessage payment = await client.PostAsJsonAsync(
            $"/api/customers/{customer.Id}/payments", new { amount = 20m, note = "Hissəvi ödəniş" });
        payment.EnsureSuccessStatusCode();
        Assert.Equal(20m, (await client.GetCustomerAsync(customer.Id)).Debt);

        HttpResponseMessage delete = await client.DeleteAsync($"/api/sales/{sale.Id}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(0m, (await client.GetCustomerAsync(customer.Id)).Debt); // floors at zero, never negative
        Assert.Equal(10, (await client.GetProductAsync(product.Id)).Quantity); // stock restored
    }

    [Fact]
    public async Task Seller_Cannot_Delete_Sale_Returns_403()
    {
        // The owner records a sale; the seller may create sales but not delete them (OwnerOrManager policy).
        HttpClient owner = await _factory.AuthenticatedClientAsync();
        var product = await owner.CreateProductAsync("SALE-DEL-403", quantity: 5, salePrice: 10m);
        var sale = await CreateSaleAsync(owner, new
        {
            productId = product.Id, quantity = 1, salePrice = 10m,
            paymentType = "Nağd", customerId = (Guid?)null
        });

        HttpClient seller = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.SellerPhone);
        HttpResponseMessage delete = await seller.DeleteAsync($"/api/sales/{sale.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(4, (await owner.GetProductAsync(product.Id)).Quantity); // nothing was unwound
    }

    [Fact]
    public async Task Update_Sale_Applies_The_Stock_Difference()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("SALE-UPD-STOCK", quantity: 20, salePrice: 10m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = product.Id, quantity = 3, salePrice = 10m,
            paymentType = "Nağd", customerId = (Guid?)null
        });
        Assert.Equal(17, (await client.GetProductAsync(product.Id)).Quantity);

        // Raise the quantity 3 → 5: reverse (+3) then reapply (−5) nets one more unit off stock.
        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/sales/{sale.Id}", new
        {
            productId = product.Id, quantity = 5, salePrice = 10m,
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
            productId = product.Id, quantity = 2, salePrice = 10m,
            paymentType = "Nağd", customerId = (Guid?)null
        });
        Assert.Equal(3, (await client.GetProductAsync(product.Id)).Quantity);

        // Reversing returns 2 (→5 available), so 6 is still one beyond what exists → fail + rollback.
        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/sales/{sale.Id}", new
        {
            productId = product.Id, quantity = 6, salePrice = 10m,
            paymentType = "Nağd", customerId = (Guid?)null
        });

        // Either a closed day (409) or the stock shortfall (400) — in both cases nothing changes.
        Assert.NotEqual(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(3, (await client.GetProductAsync(product.Id)).Quantity); // rollback / guard
        var still = (await client.GetFromJsonAsync<IntegrationTestHelpers.SaleDetailDto>(
            $"/api/sales/{sale.Id}"))!;
        Assert.Equal(2, still.Quantity); // the sale kept its original quantity
    }

    // ── PurchasePricePerUnit (BE#1): the buy price travels the whole chain, apart from the real cost ──────

    [Fact]
    public async Task Catalogued_Sale_Snapshots_Product_Purchase_Price_Beside_Its_Real_Cost()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        // Purchase price 8, plus 20 AZN of batch expenses over 10 units → real cost 10 per unit. The two
        // figures are deliberately different, so a sale that confused them would show up here.
        var product = await client.CreateProductAsync(
            "SALE-PPU-CAT", quantity: 10, salePrice: 15m, purchasePrice: 8m,
            expenses: new[] { ("Yol pulu", 20m) });
        Assert.Equal(10m, product.RealCostPerUnit);

        var sale = await CreateSaleAsync(client, new
        {
            productId = product.Id, quantity = 2, salePrice = 15m,
            paymentType = "Nağd", customerId = (Guid?)null
        });

        Assert.Equal(8m, sale.PurchasePricePerUnit); // snapshot of the product's buy price
        Assert.Equal(10m, sale.CostPerUnit);         // unchanged: still the real cost
        Assert.Equal(10m, sale.Profit);              // (15 − 10) × 2 — profit still follows the cost

        // Same pair survives the read models (list + detail), so the frontend sees both figures.
        var detail = (await client.GetFromJsonAsync<IntegrationTestHelpers.SaleDetailDto>(
            $"/api/sales/{sale.Id}"))!;
        Assert.Equal(8m, detail.PurchasePricePerUnit);
        Assert.Equal(10m, detail.CostPerUnit);
    }

    [Fact]
    public async Task Manual_Sale_Round_Trips_Supplied_Purchase_Price()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var sale = await CreateSaleAsync(client, new
        {
            productId = (Guid?)null,
            productName = "Əl ilə mal (alış qiyməti)",
            quantity = 2,
            salePrice = 200m,
            costPerUnit = 125m,           // seller's computed cost (purchase price + spread expenses)
            purchasePricePerUnit = 100m,  // the buy price itself — stored as-is, never recomputed
            paymentType = "Nağd",
            customerId = (Guid?)null,
            expenseItems = new[] { new { name = "Yol pulu", amount = 50m } }
        });

        Assert.Equal(100m, sale.PurchasePricePerUnit);
        Assert.Equal(125m, sale.CostPerUnit);
        Assert.Equal(150m, sale.Profit);   // (200 − 125) × 2 — the buy price never enters the profit formula

        var detail = (await client.GetFromJsonAsync<IntegrationTestHelpers.SaleDetailDto>(
            $"/api/sales/{sale.Id}"))!;
        Assert.Equal(100m, detail.PurchasePricePerUnit);
    }

    [Fact]
    public async Task Manual_Sale_Without_Purchase_Price_Stores_Null()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var sale = await CreateSaleAsync(client, new
        {
            productId = (Guid?)null,
            productName = "Əl ilə mal (alış qiyməti bilinmir)",
            quantity = 1,
            salePrice = 50m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });

        // Omitting the field is not an error and invents no zero — unknown stays unknown, like the cost.
        Assert.Null(sale.PurchasePricePerUnit);
        Assert.Null(sale.CostPerUnit);
        Assert.Null(sale.Profit);
    }

    [Fact]
    public async Task Negative_Purchase_Price_Returns_400_And_Writes_No_Sale()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        int before = (await client.GetFromJsonAsync<IntegrationTestHelpers.PagedSalesDto>("/api/sales?take=1"))!.Total;

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = (Guid?)null,
            productName = "Əl ilə mal (mənfi alış)",
            quantity = 1,
            salePrice = 10m,
            purchasePricePerUnit = -10m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Alış qiyməti mənfi ola bilməz", error.Message);

        int after = (await client.GetFromJsonAsync<IntegrationTestHelpers.PagedSalesDto>("/api/sales?take=1"))!.Total;
        Assert.Equal(before, after); // rejected before anything was written
    }

    [Fact]
    public async Task Update_Catalogued_Sale_Resnapshots_The_Products_Current_Purchase_Price()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync(
            "SALE-PPU-UPD", quantity: 10, salePrice: 15m, purchasePrice: 8m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = product.Id, quantity = 1, salePrice = 15m,
            paymentType = "Nağd", customerId = (Guid?)null
        });
        Assert.Equal(8m, sale.PurchasePricePerUnit);

        // The product is re-bought dearer; only an edited sale picks the new figure up.
        await client.UpdateProductAsync(
            product.Id, "SALE-PPU-UPD", quantity: 9, salePrice: 15m, purchasePrice: 12m);

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/sales/{sale.Id}", new
        {
            productId = product.Id, quantity = 1, salePrice = 15m,
            paymentType = "Nağd", customerId = (Guid?)null
        });

        var detail = (await client.GetFromJsonAsync<IntegrationTestHelpers.SaleDetailDto>(
            $"/api/sales/{sale.Id}"))!;
        if (update.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal(12m, detail.PurchasePricePerUnit); // re-snapshotted from the product
        }
        else
        {
            // The day was already closed — edits are refused and the original snapshot stands.
            Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
            Assert.Equal(8m, detail.PurchasePricePerUnit);
        }
    }

    // ── Qismən ödənişli satış (BE#15) ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC1_Partial_Credit_Payment_Splits_Paid_And_Remaining_And_Grows_Debt_By_Remaining_Only()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var customer = await client.CreateCustomerAsync("BE15 TC1 müştəri", debt: 0m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = (Guid?)null,
            productName = "BE15 TC1 mal",
            quantity = 1,
            salePrice = 500m,
            paymentType = "Nisyə",
            customerId = customer.Id,
            paidAmount = 300m,
            paidVia = "Nağd"
        });

        Assert.Equal(300m, sale.PaidAmount);
        Assert.Equal(200m, sale.RemainingAmount);
        Assert.Equal("Nisyə", sale.PaymentType); // a remaining balance is always stored as Nisyə
        Assert.Equal("Nağd", sale.PaidVia);

        Assert.Equal(200m, (await client.GetCustomerAsync(customer.Id)).Debt); // only the remaining, not 500
    }

    [Fact]
    public async Task TC2_Cash_Sale_Without_PaidAmount_Is_Fully_Paid_And_Leaves_No_Debt()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var customer = await client.CreateCustomerAsync("BE15 TC2 müştəri", debt: 0m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = (Guid?)null,
            productName = "BE15 TC2 mal",
            quantity = 1,
            salePrice = 500m,
            paymentType = "Nağd",
            customerId = customer.Id
        });

        Assert.Equal(500m, sale.PaidAmount);
        Assert.Equal(0m, sale.RemainingAmount);
        Assert.Equal("Nağd", sale.PaymentType);
        Assert.Equal(0m, (await client.GetCustomerAsync(customer.Id)).Debt);
    }

    [Fact]
    public async Task TC3_Credit_Sale_Without_PaidAmount_Defaults_To_Fully_Unpaid()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var customer = await client.CreateCustomerAsync("BE15 TC3 müştəri", debt: 0m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = (Guid?)null,
            productName = "BE15 TC3 mal",
            quantity = 1,
            salePrice = 200m,
            paymentType = "Nisyə",
            customerId = customer.Id
        });

        Assert.Equal(0m, sale.PaidAmount);
        Assert.Equal(200m, sale.RemainingAmount);
        Assert.Equal(200m, (await client.GetCustomerAsync(customer.Id)).Debt);
    }

    [Fact]
    public async Task TC4_Card_Down_Payment_On_A_Nisye_Sale_Splits_Card_And_Remaining_Debt()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var customer = await client.CreateCustomerAsync("BE15 TC4 müştəri", debt: 0m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = (Guid?)null,
            productName = "BE15 TC4 mal",
            quantity = 1,
            salePrice = 1000m,
            paymentType = "Nisyə",
            customerId = customer.Id,
            paidAmount = 600m,
            paidVia = "Kart"
        });

        Assert.Equal(600m, sale.PaidAmount);
        Assert.Equal(400m, sale.RemainingAmount);
        Assert.Equal("Nisyə", sale.PaymentType);
        Assert.Equal("Kart", sale.PaidVia);
        Assert.Equal(400m, (await client.GetCustomerAsync(customer.Id)).Debt);
    }

    [Fact]
    public async Task TC5_Zero_Paid_Cash_Sale_Without_Customer_Returns_400()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = (Guid?)null,
            productName = "BE15 TC5 mal",
            quantity = 1,
            salePrice = 150m,
            paymentType = "Nağd",
            customerId = (Guid?)null,
            paidAmount = 0m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Qalıq borc üçün müştəri seçilməlidir", error.Message);
    }

    [Fact]
    public async Task TC6_PaidAmount_Above_Total_Returns_400()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId = (Guid?)null,
            productName = "BE15 TC6 mal",
            quantity = 1,
            salePrice = 300m,
            paymentType = "Nağd",
            customerId = (Guid?)null,
            paidAmount = 350m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Ödənilən məbləğ ümumi məbləğdən çox ola bilməz", error.Message);
    }

    [Fact]
    public async Task TC8_Deleting_A_Partially_Paid_Sale_Reverses_Only_The_Remaining_Debt()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("BE15-TC8", quantity: 10, salePrice: 500m);
        var customer = await client.CreateCustomerAsync("BE15 TC8 müştəri", debt: 0m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = product.Id,
            quantity = 1,
            salePrice = 500m,
            paymentType = "Nisyə",
            customerId = customer.Id,
            paidAmount = 300m,
            paidVia = "Nağd"
        });
        Assert.Equal(200m, (await client.GetCustomerAsync(customer.Id)).Debt);
        Assert.Equal(9, (await client.GetProductAsync(product.Id)).Quantity);

        HttpResponseMessage delete = await client.DeleteAsync($"/api/sales/{sale.Id}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(0m, (await client.GetCustomerAsync(customer.Id)).Debt); // only the 200 remaining unwound
        Assert.Equal(10, (await client.GetProductAsync(product.Id)).Quantity); // stock returned
    }

    [Fact]
    public async Task TC9_Fully_Paying_Off_A_Partially_Paid_Sale_On_Update_Rewinds_The_Old_Remaining_And_Switches_To_Cash()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var customer = await client.CreateCustomerAsync("BE15 TC9 müştəri", debt: 0m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = (Guid?)null,
            productName = "BE15 TC9 mal",
            quantity = 1,
            salePrice = 500m,
            paymentType = "Nisyə",
            customerId = customer.Id,
            paidAmount = 300m,
            paidVia = "Nağd"
        });
        Assert.Equal(200m, (await client.GetCustomerAsync(customer.Id)).Debt);

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/sales/{sale.Id}", new
        {
            productId = (Guid?)null,
            productName = "BE15 TC9 mal",
            quantity = 1,
            salePrice = 500m,
            paymentType = "Nağd",
            customerId = (Guid?)null,
            paidAmount = 500m
        });

        if (update.StatusCode == HttpStatusCode.OK)
        {
            var updated = (await update.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;
            Assert.Equal(0m, updated.RemainingAmount);
            Assert.Equal("Nağd", updated.PaymentType);
            Assert.Equal(0m, (await client.GetCustomerAsync(customer.Id)).Debt); // the old 200 was rewound
        }
        else
        {
            // The day was already closed — the edit is refused and the original balance stands untouched.
            Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
            Assert.Equal(200m, (await client.GetCustomerAsync(customer.Id)).Debt);
        }
    }

    [Fact]
    public async Task TC10_Fully_Paid_Credit_Request_Never_Touches_The_Customers_Debt()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var customer = await client.CreateCustomerAsync("BE15 TC10 müştəri", debt: 0m);

        var sale = await CreateSaleAsync(client, new
        {
            productId = (Guid?)null,
            productName = "BE15 TC10 mal",
            quantity = 1,
            salePrice = 400m,
            paymentType = "Nisyə",
            customerId = customer.Id,
            paidAmount = 400m
        });

        Assert.Equal(0m, sale.RemainingAmount);
        Assert.Equal(0m, (await client.GetCustomerAsync(customer.Id)).Debt);
    }

    private static async Task<IntegrationTestHelpers.SaleDto> CreateSaleAsync(HttpClient client, object body)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!;
    }
}
