using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;
using MayaPro.WarehouseApi.Modules.Sales.Infrastructure;
using MayaPro.WarehouseApi.Modules.Settings.Infrastructure;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// BE#35 — the isolation suite. Two shops (A and B) are provisioned per test and everything is then driven
/// over the real HTTP API, so what is asserted is exactly what a customer would experience.
/// <para>
/// Two rules run through all of it: a shop only ever sees its own rows, and reaching for another shop's row
/// is a <c>404</c> — never a <c>403</c>, which would confirm the row exists.
/// </para>
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TenantIsolationApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    private TenantTestFixture.TenantHandle _shopA = null!;
    private TenantTestFixture.TenantHandle _shopB = null!;
    private HttpClient _clientA = null!;
    private HttpClient _clientB = null!;

    public TenantIsolationApiTests(WarehouseApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseResetAsync();

        _shopA = await TenantTestFixture.CreateTenantAsync("Mağaza A", TenantPhones.Next());
        _shopB = await TenantTestFixture.CreateTenantAsync("Mağaza B", TenantPhones.Next());
        _clientA = await SignInAsync(_shopA);
        _clientB = await SignInAsync(_shopB);
    }

    public Task DisposeAsync()
    {
        _clientA.Dispose();
        _clientB.Dispose();
        return Task.CompletedTask;
    }

    // --- Reads ------------------------------------------------------------------------------------

    /// <summary>TC-4 — a list endpoint returns the caller's rows and nothing else.</summary>
    [Fact]
    public async Task Product_List_Contains_Only_The_Callers_Shop()
    {
        var mine = await _clientA.CreateProductAsync(Barcode("A"), quantity: 5);
        var theirs = await _clientB.CreateProductAsync(Barcode("B"), quantity: 5);

        List<IntegrationTestHelpers.ProductDto> listA = await ProductsAsync(_clientA);

        Assert.Contains(listA, p => p.Id == mine.Id);
        Assert.DoesNotContain(listA, p => p.Id == theirs.Id);
        // Both shops are brand new, so the count is exact, not a lower bound.
        Assert.Single(listA);
    }

    /// <summary>TC-5 — reading another shop's product is a 404 that leaks none of its fields.</summary>
    [Fact]
    public async Task Cross_Tenant_Product_Read_Is_404()
    {
        var theirs = await _clientB.CreateProductAsync(Barcode("B"), quantity: 5);

        HttpResponseMessage response = await _clientA.GetAsync($"/api/products/{theirs.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(theirs.Name, body, StringComparison.Ordinal);
    }

    // --- Writes -----------------------------------------------------------------------------------

    /// <summary>TC-6 — a cross-tenant update is a 404 and the target row is untouched.</summary>
    [Fact]
    public async Task Cross_Tenant_Product_Update_Is_404_And_Changes_Nothing()
    {
        var theirs = await _clientB.CreateProductAsync(Barcode("B"), quantity: 5, salePrice: 42m);

        HttpResponseMessage response = await _clientA.PutAsJsonAsync($"/api/products/{theirs.Id}", new
        {
            name = "Oğurlanmış",
            category = "Test",
            attributes = Array.Empty<object>(),
            barcode = Barcode("X"),
            image = "",
            note = "",
            purchasePrice = 1m,
            salePrice = 1m,
            quantity = 1,
            minStock = 1,
            currency = "AZN",
            supplierId = "sup_1",
            location = "",
            store = "",
            warehouse = "",
            shelf = "",
            box = "",
            expenses = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var unchanged = await _clientB.GetProductAsync(theirs.Id);
        Assert.Equal(theirs.Name, unchanged.Name);
        Assert.Equal(5, unchanged.Quantity);
    }

    /// <summary>TC-7 — a cross-tenant delete is a 404 and the row survives.</summary>
    [Fact]
    public async Task Cross_Tenant_Product_Delete_Is_404_And_Row_Survives()
    {
        var theirs = await _clientB.CreateProductAsync(Barcode("B"), quantity: 5);

        HttpResponseMessage response = await _clientA.DeleteAsync($"/api/products/{theirs.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(await ProductsAsync(_clientB), p => p.Id == theirs.Id);
    }

    /// <summary>TC-8 — the same rule across the other modules: sales, customers and suppliers.</summary>
    [Fact]
    public async Task Cross_Tenant_Sale_Customer_And_Supplier_Are_404()
    {
        var productB = await _clientB.CreateProductAsync(Barcode("B"), quantity: 5, salePrice: 10m);
        var customerB = await _clientB.CreateCustomerAsync("B müştərisi", debt: 50m);
        var supplierB = await _clientB.CreateSupplierAsync("B təchizatçısı", debt: 20m);
        Guid saleB = await SellAsync(_clientB, productB.Id, quantity: 1, price: 10m);

        Assert.Equal(HttpStatusCode.NotFound, (await _clientA.GetAsync($"/api/sales/{saleB}")).StatusCode);

        HttpResponseMessage payment = await _clientA.PostAsJsonAsync(
            $"/api/customers/{customerB.Id}/payments", new { amount = 10m, note = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, payment.StatusCode);

        HttpResponseMessage supplierPayment = await _clientA.PostAsJsonAsync(
            $"/api/suppliers/{supplierB.Id}/payments", new { amount = 10m, note = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, supplierPayment.StatusCode);

        HttpResponseMessage renameCustomer = await _clientA.PutAsJsonAsync(
            $"/api/customers/{customerB.Id}",
            new { name = "Oğurlanmış müştəri", phone = (string?)null, note = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, renameCustomer.StatusCode);

        // The read-only history feeds simply come back empty for a foreign id — no row of B's leaks.
        List<IntegrationTestHelpers.CustomerHistoryEntryDto> history =
            (await _clientA.GetFromJsonAsync<List<IntegrationTestHelpers.CustomerHistoryEntryDto>>(
                $"/api/customers/{customerB.Id}/history"))!;
        Assert.Empty(history);

        // Nothing moved on B's side.
        var customerAfter = await _clientB.GetCustomerAsync(customerB.Id);
        Assert.Equal(50m, customerAfter.Debt);
        Assert.Equal("B müştərisi", customerAfter.Name);
        Assert.Equal(20m, (await _clientB.GetSupplierAsync(supplierB.Id)).Debt);
    }

    /// <summary>TC-9 / TC-10 — the interceptor stamps the caller's tenant, and a spoofed body cannot move it.</summary>
    [Fact]
    public async Task New_Row_Belongs_To_The_Caller_Even_When_The_Body_Claims_Otherwise()
    {
        HttpResponseMessage created = await _clientA.PostAsJsonAsync("/api/products", new
        {
            name = "Tenant testi",
            category = "Test",
            attributes = Array.Empty<object>(),
            barcode = Barcode("A"),
            image = "",
            note = "",
            purchasePrice = 5m,
            salePrice = 10m,
            quantity = 3,
            minStock = 1,
            currency = "AZN",
            supplierId = "sup_1",
            location = "",
            store = "",
            warehouse = "",
            shelf = "",
            box = "",
            expenses = Array.Empty<object>(),
            // The spoof: an explicit foreign tenant in the request body.
            tenantId = _shopB.TenantId
        });

        Assert.True(created.IsSuccessStatusCode, $"gözlənilən uğur, gəldi {created.StatusCode}");
        var product = (await created.Content.ReadFromJsonAsync<IntegrationTestHelpers.ProductDto>())!;

        Guid storedTenant = await TenantTestFixture.QueryAcrossTenantsAsync<ProductsDbContext, Guid>(
            o => new ProductsDbContext(o),
            db => db.Products.IgnoreQueryFilters()
                .Where(p => p.Id == product.Id)
                .Select(p => p.TenantId)
                .SingleAsync());

        Assert.Equal(_shopA.TenantId, storedTenant);
        Assert.DoesNotContain(await ProductsAsync(_clientB), p => p.Id == product.Id);
    }

    /// <summary>
    /// TC-11 — even code holding the entity cannot move a row between shops: the interceptor reverts the
    /// change instead of letting SaveChanges write it.
    /// </summary>
    [Fact]
    public async Task Tenant_Of_An_Existing_Row_Cannot_Be_Changed()
    {
        var product = await _clientA.CreateProductAsync(Barcode("A"), quantity: 5);

        await using (ProductsDbContext db = TenantAwareProductsDb(_shopA.TenantId))
        {
            Product row = await db.Products.SingleAsync(p => p.Id == product.Id);
            row.AssignTenant(_shopB.TenantId);
            await db.SaveChangesAsync();
        }

        Guid storedTenant = await TenantTestFixture.QueryAcrossTenantsAsync<ProductsDbContext, Guid>(
            o => new ProductsDbContext(o),
            db => db.Products.IgnoreQueryFilters()
                .Where(p => p.Id == product.Id)
                .Select(p => p.TenantId)
                .SingleAsync());

        Assert.Equal(_shopA.TenantId, storedTenant);
        Assert.Contains(await ProductsAsync(_clientA), p => p.Id == product.Id);
    }

    /// <summary>
    /// AC-7 — with no tenant context at all, a tenant-scoped insert fails loudly rather than writing a row
    /// that belongs to nobody.
    /// </summary>
    [Fact]
    public async Task Insert_Without_Tenant_Context_Throws()
    {
        await using ProductsDbContext db = TenantAwareProductsDb(null);

        db.Products.Add(Product.Create(
            "Sahibsiz mal", "Test", [], Barcode("N"), "", "", 1m, 2m, 1, 1, "AZN", "sup_1", "", "", "", "", "", []));

        await Assert.ThrowsAsync<MissingTenantContextException>(() => db.SaveChangesAsync());
    }

    // --- Unique indexes ---------------------------------------------------------------------------

    /// <summary>TC-12 / TC-13 — a barcode is unique inside a shop, and free to repeat across shops.</summary>
    [Fact]
    public async Task Barcode_Is_Unique_Per_Shop_Only()
    {
        string barcode = Barcode("SHARED");

        await _clientA.CreateProductAsync(barcode, quantity: 5);

        // Same barcode, different shop → allowed.
        HttpResponseMessage inB = await _clientB.PostAsJsonAsync("/api/products", ProductBody(barcode));
        Assert.True(inB.IsSuccessStatusCode, $"B mağazasında eyni barkod qəbul edilməli idi, gəldi {inB.StatusCode}");

        // Same barcode, same shop → a business error, never a 500 from the constraint.
        HttpResponseMessage duplicate = await _clientA.PostAsJsonAsync("/api/products", ProductBody(barcode));
        Assert.True(
            duplicate.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"400/409 gözlənilirdi, gəldi {duplicate.StatusCode}");
    }

    /// <summary>TC-14 — category and expense-type names are unique per shop.</summary>
    [Fact]
    public async Task Category_And_Expense_Type_Names_Are_Unique_Per_Shop_Only()
    {
        const string category = "İçkilər";
        const string expenseType = "Kirayə";

        Assert.True((await _clientA.PostAsJsonAsync("/api/categories", new { name = category })).IsSuccessStatusCode);
        Assert.True((await _clientB.PostAsJsonAsync("/api/categories", new { name = category })).IsSuccessStatusCode);
        HttpResponseMessage duplicateCategory =
            await _clientA.PostAsJsonAsync("/api/categories", new { name = category });
        Assert.True(duplicateCategory.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);

        Assert.True((await _clientA.PostAsJsonAsync("/api/expense-types", new { name = expenseType })).IsSuccessStatusCode);
        Assert.True((await _clientB.PostAsJsonAsync("/api/expense-types", new { name = expenseType })).IsSuccessStatusCode);
        HttpResponseMessage duplicateType =
            await _clientA.PostAsJsonAsync("/api/expense-types", new { name = expenseType });
        Assert.True(duplicateType.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
    }

    /// <summary>TC-15 — each shop closes its own day; the same calendar day can be closed once per shop.</summary>
    [Fact]
    public async Task Day_Can_Be_Closed_Once_Per_Shop()
    {
        HttpResponseMessage firstA = await _clientA.PostAsJsonAsync(
            "/api/closings", new { openingCash = 0m, actualCash = 0m, note = "A" });
        Assert.Equal(HttpStatusCode.Created, firstA.StatusCode);

        HttpResponseMessage firstB = await _clientB.PostAsJsonAsync(
            "/api/closings", new { openingCash = 0m, actualCash = 0m, note = "B" });
        Assert.Equal(HttpStatusCode.Created, firstB.StatusCode);

        HttpResponseMessage secondA = await _clientA.PostAsJsonAsync(
            "/api/closings", new { openingCash = 0m, actualCash = 0m, note = "A yenidən" });
        Assert.True(
            secondA.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"400/409 gözlənilirdi, gəldi {secondA.StatusCode}");
    }

    // --- Cross-module chains ----------------------------------------------------------------------

    /// <summary>
    /// TC-21 — the most important one: a sale that reaches for another shop's product must fail before the
    /// chain moves anything, in either shop.
    /// </summary>
    [Fact]
    public async Task Sale_Against_Another_Shops_Product_Is_404_And_Moves_No_Stock()
    {
        var productB = await _clientB.CreateProductAsync(Barcode("B"), quantity: 7, salePrice: 10m);
        var customerB = await _clientB.CreateCustomerAsync("B müştərisi", debt: 0m);

        HttpResponseMessage response = await _clientA.PostAsJsonAsync("/api/sales", new
        {
            productId = productB.Id,
            quantity = 2,
            salePrice = 10m,
            paymentType = "Nisyə",
            customerId = customerB.Id
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(7, (await _clientB.GetProductAsync(productB.Id)).Quantity);
        Assert.Equal(0m, (await _clientB.GetCustomerAsync(customerB.Id)).Debt);
        Assert.Empty((await SalesAsync(_clientA)).Items);
    }

    /// <summary>TC-22 — the expense → real-cost chain is scoped the same way.</summary>
    [Fact]
    public async Task Expense_Against_Another_Shops_Product_Is_404_And_Leaves_Its_Cost_Alone()
    {
        var productB = await _clientB.CreateProductAsync(Barcode("B"), quantity: 10, salePrice: 20m);
        decimal costBefore = (await _clientB.GetProductAsync(productB.Id)).RealCostPerUnit;

        HttpResponseMessage response = await _clientA.PostAsJsonAsync("/api/expenses", new
        {
            title = "Karqo",
            category = "Yol pulu",
            source = "product",
            amount = 100m,
            date = (DateTime?)null,
            productId = productB.Id,
            note = (string?)null
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(costBefore, (await _clientB.GetProductAsync(productB.Id)).RealCostPerUnit);
    }

    // --- Aggregates and side surfaces --------------------------------------------------------------

    /// <summary>TC-18 — settings are per shop; one shop's update never reaches the other.</summary>
    [Fact]
    public async Task Settings_Are_Per_Shop()
    {
        HttpResponseMessage put = await _clientA.PutAsJsonAsync("/api/settings", SettingsBody("Mağaza A adı"));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var settingsB = (await _clientB.GetFromJsonAsync<IntegrationTestHelpers.SettingsDto>("/api/settings"))!;
        Assert.NotEqual("Mağaza A adı", settingsB.StoreName);

        var settingsA = (await _clientA.GetFromJsonAsync<IntegrationTestHelpers.SettingsDto>("/api/settings"))!;
        Assert.Equal("Mağaza A adı", settingsA.StoreName);

        // Two physical rows, one per shop.
        int rows = await TenantTestFixture.QueryAcrossTenantsAsync<SettingsDbContext, int>(
            o => new SettingsDbContext(o),
            db => db.StoreSettings.IgnoreQueryFilters()
                .CountAsync(s => s.TenantId == _shopA.TenantId || s.TenantId == _shopB.TenantId));
        Assert.Equal(2, rows);
    }

    /// <summary>TC-19 — reports aggregate the caller's shop only.</summary>
    [Fact]
    public async Task Reports_Only_Count_The_Callers_Shop()
    {
        var productA = await _clientA.CreateProductAsync(Barcode("A"), quantity: 10, salePrice: 100m);
        var productB = await _clientB.CreateProductAsync(Barcode("B"), quantity: 10, salePrice: 500m);
        await SellAsync(_clientA, productA.Id, quantity: 1, price: 100m);
        await SellAsync(_clientB, productB.Id, quantity: 1, price: 500m);

        var summaryA = (await _clientA.GetFromJsonAsync<IntegrationTestHelpers.SummaryDto>(
            "/api/reports/summary?period=today"))!;
        var dashboardA = (await _clientA.GetFromJsonAsync<IntegrationTestHelpers.DashboardDto>(
            "/api/reports/dashboard"))!;

        Assert.Equal(100m, summaryA.SalesTotal);
        Assert.Equal(1, summaryA.SalesCount);
        Assert.Equal(100m, dashboardA.TodaySales);
        Assert.Equal(1, dashboardA.ProductCount);
    }

    /// <summary>TC-20 — the activity feed is scoped too.</summary>
    [Fact]
    public async Task Activity_Feed_Is_Scoped()
    {
        var productA = await _clientA.CreateProductAsync(Barcode("A"), quantity: 4, salePrice: 10m);
        var productB = await _clientB.CreateProductAsync(Barcode("B"), quantity: 4, salePrice: 10m);
        await SellAsync(_clientA, productA.Id, quantity: 1, price: 10m);
        await SellAsync(_clientB, productB.Id, quantity: 1, price: 10m);

        List<IntegrationTestHelpers.ActivityDto> feedA =
            (await _clientA.GetFromJsonAsync<List<IntegrationTestHelpers.ActivityDto>>("/api/activity"))!;
        List<IntegrationTestHelpers.ActivityDto> feedB =
            (await _clientB.GetFromJsonAsync<List<IntegrationTestHelpers.ActivityDto>>("/api/activity"))!;

        Assert.NotEmpty(feedA);
        Assert.NotEmpty(feedB);
        Assert.Empty(feedA.Select(a => a.Id).Intersect(feedB.Select(a => a.Id)));
    }

    /// <summary>TC-26 — an export carries the caller's rows only.</summary>
    [Fact]
    public async Task Product_Export_Only_Contains_The_Callers_Shop()
    {
        string barcodeA = Barcode("A");
        string barcodeB = Barcode("B");
        await _clientA.CreateProductAsync(barcodeA, quantity: 3);
        await _clientB.CreateProductAsync(barcodeB, quantity: 3);

        HttpResponseMessage export = await _clientA.GetAsync("/api/exports/products.xlsx");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);

        // The workbook is a zip, so it has to be opened rather than scanned — a raw byte search would
        // "pass" on compressed content whether or not B's row was in there.
        await using var stream = new MemoryStream(await export.Content.ReadAsByteArrayAsync());
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        List<string> cells = workbook.Worksheet(1).CellsUsed().Select(c => c.GetString()).ToList();

        Assert.Contains(barcodeA, cells);
        Assert.DoesNotContain(barcodeB, cells);
    }

    // --- Anonymous and token surfaces --------------------------------------------------------------

    /// <summary>
    /// TC-23 — the anonymous invoice link still works with no JWT at all, and resolves its tenant from the
    /// token: two shops' invoices render from their own data, and neither can be fetched via the other.
    /// </summary>
    [Fact]
    public async Task Public_Invoice_Resolves_Its_Shop_From_The_Token()
    {
        var productA = await _clientA.CreateProductAsync(Barcode("A"), quantity: 5, salePrice: 20m);
        Guid saleA = await SellAsync(_clientA, productA.Id, quantity: 1, price: 20m);
        string tokenA = await InvoiceTokenAsync(_clientA, saleA);

        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage pdf = await anonymous.GetAsync($"/api/public/invoices/{tokenA}");

        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        byte[] bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));

        // The authenticated invoice for the same sale stays scoped: shop B cannot export it.
        Assert.Equal(HttpStatusCode.NotFound,
            (await _clientB.GetAsync($"/api/exports/sales/{saleA}/invoice.pdf")).StatusCode);

        // And the token really does belong to A's sale.
        Guid ownerTenant = await TenantTestFixture.QueryAcrossTenantsAsync<SalesDbContext, Guid>(
            o => new SalesDbContext(o),
            db => db.Sales.IgnoreQueryFilters()
                .Where(s => s.InvoiceToken == tokenA)
                .Select(s => s.TenantId)
                .SingleAsync());
        Assert.Equal(_shopA.TenantId, ownerTenant);
    }

    /// <summary>
    /// TC-24 — a validly signed token from before multi-tenancy carries no <c>tenantId</c>. It must be
    /// rejected outright rather than quietly resolving to "no shop" (or, worse, all of them).
    /// </summary>
    [Fact]
    public async Task Token_Without_A_Tenant_Claim_Is_Rejected()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", LegacyTokenWithoutTenant(_shopA.OwnerUserId));

        HttpResponseMessage response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// TC-25 / AC-9 — a blocked shop cannot sign in, and the token it already holds stops working on the
    /// very next request.
    /// </summary>
    [Fact]
    public async Task Blocked_Shop_Cannot_Sign_In_And_Its_Existing_Token_Stops_Working()
    {
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("Bloklanmış mağaza", TenantPhones.Next());
        HttpClient issued = await SignInAsync(shop);
        Assert.Equal(HttpStatusCode.OK, (await issued.GetAsync("/api/products")).StatusCode);

        await TenantTestFixture.SetStatusAsync(shop.TenantId, TenantStatus.Blocked);

        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage login = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { phone = shop.OwnerPhone, password = shop.OwnerPassword });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        var error = (await login.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        // BE#36 replaced the generic "Mağaza aktiv deyil" for this state with a message that tells the
        // customer what happened and who to call. The generic one now only answers an unknown tenant.
        Assert.Equal("Auth.TenantBlockedForbidden", error.Code);
        Assert.StartsWith("Abunəliyiniz bitib", error.Message, StringComparison.Ordinal);

        HttpResponseMessage afterBlock = await issued.GetAsync("/api/products");
        Assert.True(
            afterBlock.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"401/403 gözlənilirdi, gəldi {afterBlock.StatusCode}");

        issued.Dispose();
    }

    /// <summary>
    /// TC-16 — the same phone in two shops with different passwords: the password decides which shop the
    /// caller lands in, deterministically and without a 500.
    /// </summary>
    [Fact]
    public async Task Same_Phone_In_Two_Shops_Signs_Into_The_Matching_One()
    {
        string phone = TenantPhones.Next();
        await TenantTestFixture.AddUserAsync(_shopA.TenantId, "A sahibi", phone, "parolA123", UserRoleForTest);
        await TenantTestFixture.AddUserAsync(_shopB.TenantId, "B sahibi", phone, "parolB123", UserRoleForTest);

        Assert.Equal(_shopA.TenantId, await TenantOfLoginAsync(phone, "parolA123"));
        Assert.Equal(_shopB.TenantId, await TenantOfLoginAsync(phone, "parolB123"));

        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage wrong = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { phone, password = "tamamile-yanlis" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
    }

    /// <summary>
    /// TC-17 — the same phone <i>and</i> the same password in two shops is genuinely ambiguous. Guessing
    /// would drop the caller into someone else's shop, so the answer is a plain "wrong credentials".
    /// </summary>
    [Fact]
    public async Task Same_Phone_And_Password_In_Two_Shops_Is_Rejected()
    {
        string phone = TenantPhones.Next();
        const string password = "eyniParol123";
        await TenantTestFixture.AddUserAsync(_shopA.TenantId, "A sahibi", phone, password, UserRoleForTest);
        await TenantTestFixture.AddUserAsync(_shopB.TenantId, "B sahibi", phone, password, UserRoleForTest);

        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage login = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { phone, password });

        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
        var error = (await login.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Telefon və ya şifrə yanlışdır", error.Message);
    }

    // --- Helpers ----------------------------------------------------------------------------------

    private const UserRole UserRoleForTest = UserRole.Owner;

    private async Task<HttpClient> SignInAsync(TenantTestFixture.TenantHandle shop) =>
        await _factory.AuthenticatedClientAsync(shop.OwnerPhone, shop.OwnerPassword);

    private async Task<Guid> TenantOfLoginAsync(string phone, string password)
    {
        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage login = await anonymous.PostAsJsonAsync("/api/auth/login", new { phone, password });
        login.EnsureSuccessStatusCode();

        var body = (await login.Content.ReadFromJsonAsync<IntegrationTestHelpers.LoginDto>())!;
        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.Token);
        return Guid.Parse(jwt.Claims.Single(c => c.Type == "tenantId").Value);
    }

    private static async Task<List<IntegrationTestHelpers.ProductDto>> ProductsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<IntegrationTestHelpers.ProductDto>>("/api/products"))!;

    private static async Task<IntegrationTestHelpers.PagedSalesDto> SalesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<IntegrationTestHelpers.PagedSalesDto>("/api/sales"))!;

    private static async Task<Guid> SellAsync(HttpClient client, Guid productId, int quantity, decimal price)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sales", new
        {
            productId,
            quantity,
            salePrice = price,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.SaleDto>())!.Id;
    }

    private static async Task<string> InvoiceTokenAsync(HttpClient client, Guid saleId)
    {
        HttpResponseMessage response = await client.PostAsync($"/api/sales/{saleId}/invoice-link", null);
        response.EnsureSuccessStatusCode();
        var link = (await response.Content.ReadFromJsonAsync<InvoiceLinkDto>())!;
        return link.Url.Split('/').Last();
    }

    /// <summary>
    /// A Products context wired exactly like the host's: the shared tenant interceptor plus a fixed tenant.
    /// This is how the "cannot move a row between shops" and "no tenant, no insert" rules are exercised at
    /// the level they are enforced at.
    /// </summary>
    private static ProductsDbContext TenantAwareProductsDb(Guid? tenantId)
    {
        var currentTenant = new FixedTenant(tenantId);
        DbContextOptions<ProductsDbContext> options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseSqlServer(WarehouseApiFactory.TestConnectionString)
            .AddInterceptors(new TenantInterceptor(currentTenant))
            .Options;

        return new ProductsDbContext(options, currentTenant);
    }

    private static string LegacyTokenWithoutTenant(Guid userId)
    {
        // Same issuer/audience/secret as appsettings.json, so the signature validates and only the missing
        // tenantId claim can be the reason the request is refused.
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                "dev_only_super_secret_signing_key_change_in_production_32+chars")),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "MayaPro.WarehouseApi",
            audience: "MayaPro.WarehouseApi.Client",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim("name", "Köhnə token"),
                new Claim("role", "Owner"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static object ProductBody(string barcode) => new
    {
        name = "Barkod testi",
        category = "Test",
        attributes = Array.Empty<object>(),
        barcode,
        image = "",
        note = "",
        purchasePrice = 5m,
        salePrice = 10m,
        quantity = 2,
        minStock = 1,
        currency = "AZN",
        supplierId = "sup_1",
        location = "",
        store = "",
        warehouse = "",
        shelf = "",
        box = "",
        expenses = Array.Empty<object>()
    };

    private static object SettingsBody(string storeName) => new
    {
        storeName,
        ownerName = "Sahib",
        address = (string?)null,
        phone = (string?)null,
        whatsappTemplate = "Salam, borcunuz {debt} AZN.",
        currency = "AZN",
        defaultMinStock = 10,
        language = "az"
    };

    /// <summary>Unique per test run, so a barcode never collides with another test's row.</summary>
    private static string Barcode(string prefix) => $"TEN-{prefix}-{Guid.NewGuid():N}"[..24];

    private sealed record InvoiceLinkDto(string Url);

    private sealed class FixedTenant(Guid? tenantId) : ICurrentTenant
    {
        public Guid? TenantId => tenantId;

        public bool HasTenant => tenantId is not null;
    }
}

/// <summary>
/// Hands out a fresh login phone for every shop a test provisions. Phones are only unique <i>within</i> a
/// shop now, so reusing one across shops would make the anonymous login ambiguous — exactly the case
/// <see cref="TenantIsolationApiTests.Same_Phone_And_Password_In_Two_Shops_Is_Rejected"/> pins down on
/// purpose, and the last thing every other test wants by accident.
/// </summary>
internal static class TenantPhones
{
    private static int _sequence;

    public static string Next() => $"07{Interlocked.Increment(ref _sequence):D8}";
}
