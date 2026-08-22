using System.Net;
using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// BE#46, AC-3..AC-7 — the canonical phone rule as seen from outside: every endpoint that accepts a phone
/// stores it as <c>994XXXXXXXXX</c>, refuses anything it cannot read with one frozen message, and lets a
/// person sign in typing the number however they like.
/// <para>
/// The migration side of the same rule lives in <c>PhoneNormalizationMigrationTests</c>; this class only
/// exercises the running API.
/// </para>
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PhoneFormatApiTests(WarehouseApiFactory factory) : IAsyncLifetime
{
    /// <summary>The refusal, quoted verbatim — the text is part of the contract, not a detail.</summary>
    private const string FormatMessage = "Telefon nömrəsi düzgün formatda deyil (məs: 050 123 45 67)";

    private readonly WarehouseApiFactory _factory = factory;
    private HttpClient _owner = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseResetAsync();
        _owner = await _factory.AuthenticatedClientAsync();
    }

    public Task DisposeAsync()
    {
        _owner.Dispose();
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------- customers

    /// <summary>TC-15 — created with a human spelling, read back canonical, both in the response and later.</summary>
    [Theory]
    [InlineData("050 123 45 67", "994501234567")]
    [InlineData("+994 (55) 111-22-33", "994551112233")]
    [InlineData("0701234567", "994701234567")]
    [InlineData("994121234567", "994121234567")]
    public async Task A_Customers_Phone_Is_Stored_Canonically(string typed, string canonical)
    {
        HttpResponseMessage response = await _owner.PostAsJsonAsync(
            "/api/customers", new { name = "Telefon Müştərisi", phone = typed });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        CustomerDto created = (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
        Assert.Equal(canonical, created.Phone);

        // And it is the stored value, not a one-off formatting of the response.
        List<CustomerDto> all = (await _owner.GetFromJsonAsync<List<CustomerDto>>("/api/customers"))!;
        Assert.Equal(canonical, all.Single(c => c.Id == created.Id).Phone);
    }

    /// <summary>TC-17 — an optional phone left out stays out; it must not become an empty string.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_Customer_Without_A_Phone_Is_Created_With_None(string? typed)
    {
        HttpResponseMessage response = await _owner.PostAsJsonAsync(
            "/api/customers", new { name = "Telefonsuz Müştəri", phone = typed });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        CustomerDto created = (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
        Assert.Null(created.Phone);
    }

    /// <summary>TC-16 — a phone nobody can read is a 400, and the row it would have changed is untouched.</summary>
    [Theory]
    [InlineData("12345")]
    [InlineData("501234567")]
    [InlineData("00994501234567")]
    [InlineData("abc")]
    public async Task An_Unreadable_Phone_Is_Refused_And_Changes_Nothing(string typed)
    {
        CustomerDto customer = await CreateCustomerAsync("994501119999");

        HttpResponseMessage response = await _owner.PutAsJsonAsync(
            $"/api/customers/{customer.Id}",
            new { name = "Dəyişdirilmiş ad", phone = typed, note = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        IntegrationTestHelpers.ErrorDto error =
            (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal(FormatMessage, error.Message);
        Assert.Equal("General.Validation", error.Code);

        // Nothing was written — not the phone, and not the name that shared the request.
        List<CustomerDto> all = (await _owner.GetFromJsonAsync<List<CustomerDto>>("/api/customers"))!;
        CustomerDto reread = all.Single(c => c.Id == customer.Id);
        Assert.Equal("994501119999", reread.Phone);
        Assert.Equal(customer.Name, reread.Name);
    }

    /// <summary>The same refusal on create, so a bad phone never reaches the table by either door.</summary>
    [Fact]
    public async Task An_Unreadable_Phone_Is_Refused_On_Create_Too()
    {
        HttpResponseMessage response = await _owner.PostAsJsonAsync(
            "/api/customers", new { name = "Pis Telefon", phone = "12345" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        IntegrationTestHelpers.ErrorDto error =
            (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal(FormatMessage, error.Message);

        List<CustomerDto> all = (await _owner.GetFromJsonAsync<List<CustomerDto>>("/api/customers"))!;
        Assert.DoesNotContain(all, c => c.Name == "Pis Telefon");
    }

    // ---------------------------------------------------------------- suppliers

    /// <summary>TC-18 — suppliers follow the identical rule on create and on edit.</summary>
    [Fact]
    public async Task A_Suppliers_Phone_Is_Canonical_On_Create_And_On_Update()
    {
        HttpResponseMessage created = await _owner.PostAsJsonAsync(
            "/api/suppliers", new { name = "Telefon Təchizatçısı", phone = "+994 50 111 22 33" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        SupplierDto supplier = (await created.Content.ReadFromJsonAsync<SupplierDto>())!;
        Assert.Equal("994501112233", supplier.Phone);

        HttpResponseMessage updated = await _owner.PutAsJsonAsync(
            $"/api/suppliers/{supplier.Id}",
            new { name = supplier.Name, contactName = (string?)null, phone = "055 444 33 22", note = (string?)null });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("994554443322", (await updated.Content.ReadFromJsonAsync<SupplierDto>())!.Phone);
    }

    [Fact]
    public async Task An_Unreadable_Supplier_Phone_Is_Refused()
    {
        HttpResponseMessage response = await _owner.PostAsJsonAsync(
            "/api/suppliers", new { name = "Pis Telefon Təchizatçısı", phone = "12345" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            FormatMessage,
            (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!.Message);
    }

    // ---------------------------------------------------------------- settings

    /// <summary>TC-19 — the store phone printed on invoices, refused when unreadable.</summary>
    [Fact]
    public async Task An_Unreadable_Store_Phone_Is_Refused()
    {
        HttpResponseMessage response = await _owner.PutAsJsonAsync("/api/settings", new
        {
            storeName = "Telefon Mağazası",
            ownerName = (string?)null,
            address = (string?)null,
            phone = "12345",
            whatsappTemplate = "Salam {debt}",
            currency = "AZN",
            defaultMinStock = 10,
            language = "az"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            FormatMessage,
            (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!.Message);
    }

    // ---------------------------------------------------------------- registration

    /// <summary>
    /// TC-20 — registration writes two rows in two schemas (<c>tenancy.Tenants</c>, <c>identity.Users</c>);
    /// both must carry the same canonical string, or login would find a user the admin list cannot match.
    /// </summary>
    [Fact]
    public async Task Registration_Stores_The_Owner_Phone_Canonically_In_Both_Schemas()
    {
        // One number, three spellings: registered local, listed canonical, signed in with "+994 …".
        string subscriber = Random.Shared.Next(10000000, 99999999).ToString();
        string typed = $"05{subscriber[..1]} {subscriber[1..4]} {subscriber[4..6]} {subscriber[6..]}";
        string canonical = $"9945{subscriber}";

        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage response = await anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            storeName = "Kanonik Telefon Mağazası",
            ownerName = "Kanonik Sahibkar",
            phone = typed,
            password = "demo123"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        RegistrationDto registration = (await response.Content.ReadFromJsonAsync<RegistrationDto>())!;

        using HttpClient admin = await _factory.PlatformAdminClientAsync();
        List<TenantRow> rows = (await admin.GetFromJsonAsync<List<TenantRow>>(
            "/api/admin/tenants?search=Kanonik Telefon Mağazası"))!;

        Assert.Equal(canonical, rows.Single(r => r.Id == registration.TenantId).Phone);

        // The owner's user row agrees — proved by signing in with a third spelling entirely.
        await admin.PostAsJsonAsync($"/api/admin/tenants/{registration.TenantId}/approve", new { periodMonths = 1 });
        using HttpClient owner = await _factory.AuthenticatedClientAsync($"+994 5{subscriber}", "demo123");
        MeDto me = (await owner.GetFromJsonAsync<MeDto>("/api/auth/me"))!;
        Assert.Equal(canonical, me.Phone);
    }

    /// <summary>TC-22 — an unreadable phone stops registration before either row exists.</summary>
    [Fact]
    public async Task Registration_With_An_Unreadable_Phone_Creates_Nothing()
    {
        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage response = await anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            storeName = "Yaranmamalı Mağaza",
            ownerName = "Heç Kim",
            phone = "abc",
            password = "demo123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            FormatMessage,
            (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!.Message);

        using HttpClient admin = await _factory.PlatformAdminClientAsync();
        List<TenantRow> rows = (await admin.GetFromJsonAsync<List<TenantRow>>(
            "/api/admin/tenants?search=Yaranmamalı Mağaza"))!;
        Assert.Empty(rows);
    }

    /// <summary>
    /// TC-30, AC-7 — the duplicate-phone rule now compares canonical values, so re-registering the same
    /// number in a different spelling is caught (409) instead of quietly creating a second account that
    /// would then make login ambiguous for both of them.
    /// </summary>
    [Fact]
    public async Task Re_Registering_The_Same_Number_In_Another_Spelling_Is_A_Conflict()
    {
        string canonical = "9945" + Random.Shared.Next(10000000, 99999999);
        using HttpClient anonymous = _factory.CreateClient();

        HttpResponseMessage first = await anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            storeName = "Təkrar Mağaza Bir",
            ownerName = "Birinci",
            phone = canonical,
            password = "demo123"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Same number, written the way a person would type it.
        string typed = $"+994 ({canonical[3..5]}) {canonical[5..8]}-{canonical[8..10]}-{canonical[10..]}";

        HttpResponseMessage second = await anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            storeName = "Təkrar Mağaza İki",
            ownerName = "İkinci",
            phone = typed,
            password = "demo123"
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        IntegrationTestHelpers.ErrorDto error =
            (await second.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Tenancy.PhoneAlreadyExists", error.Code);
        Assert.Equal("Bu telefon nömrəsi artıq qeydiyyatdadır", error.Message);

        using HttpClient admin = await _factory.PlatformAdminClientAsync();
        Assert.Empty((await admin.GetFromJsonAsync<List<TenantRow>>(
            "/api/admin/tenants?search=Təkrar Mağaza İki"))!);
    }

    /// <summary>TC-21 — the admin's own "create a shop" door applies the same rule.</summary>
    [Fact]
    public async Task Admin_Created_Shops_Store_The_Owner_Phone_Canonically()
    {
        string suffix = Random.Shared.Next(10000000, 99999999).ToString();
        using HttpClient admin = await _factory.PlatformAdminClientAsync();

        HttpResponseMessage response = await admin.PostAsJsonAsync("/api/admin/tenants", new
        {
            storeName = "Admin Telefon Mağazası",
            ownerName = "Admin Sahibkar",
            phone = $"05{suffix}",
            password = "demo123",
            periodMonths = 1,
            monthlyFee = 10m
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        TenantRow created = (await response.Content.ReadFromJsonAsync<TenantRow>())!;
        Assert.Equal($"9945{suffix}", created.Phone);
    }

    // ---------------------------------------------------------------- login

    /// <summary>
    /// TC-24, TC-25 — the seeded owner is stored as <c>994501112233</c>; every spelling of that number signs
    /// the same person in. This is the compatibility promise of the whole task.
    /// </summary>
    [Theory]
    [InlineData("0501112233")]
    [InlineData("050 111 22 33")]
    [InlineData("+994 50 111 22 33")]
    [InlineData("994501112233")]
    [InlineData("(050) 111-22-33")]
    public async Task The_Seeded_Owner_Signs_In_With_Any_Spelling(string typed)
    {
        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage response = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { phone = typed, password = IntegrationTestHelpers.DemoPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// TC-26 — an unreadable phone at login gets the neutral credentials message, never the format one. The
    /// difference between the two answers would itself be information about which numbers exist.
    /// </summary>
    [Theory]
    [InlineData("12345")]
    [InlineData("abc")]
    [InlineData("501112233")]
    public async Task An_Unreadable_Phone_At_Login_Is_Answered_Neutrally(string typed)
    {
        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage response = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { phone = typed, password = "nese" });

        IntegrationTestHelpers.ErrorDto error =
            (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;

        Assert.Equal("Telefon və ya şifrə yanlışdır", error.Message);
        Assert.NotEqual(FormatMessage, error.Message);
    }

    /// <summary>TC-27 — a blank phone keeps its own, older message: it leaks nothing.</summary>
    [Fact]
    public async Task An_Empty_Phone_At_Login_Still_Says_The_Field_Is_Empty()
    {
        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage response = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { phone = "", password = "nese" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "Telefon boş ola bilməz",
            (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!.Message);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<CustomerDto> CreateCustomerAsync(string phone)
    {
        HttpResponseMessage response = await _owner.PostAsJsonAsync(
            "/api/customers", new { name = "Telefon Testi " + Guid.NewGuid().ToString()[..8], phone });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
    }

    private sealed record CustomerDto(Guid Id, string Name, string? Phone, string? Note, decimal Debt);

    private sealed record SupplierDto(Guid Id, string Name, string? ContactName, string? Phone, decimal Debt);

    private sealed record RegistrationDto(Guid TenantId, string StoreName, string Status, string Message);

    private sealed record TenantRow(Guid Id, string Name, string? OwnerName, string? Phone, string Status);

    private sealed record MeDto(Guid Id, string FullName, string Phone, string Role);
}
