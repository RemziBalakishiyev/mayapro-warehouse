using System.Net;
using System.Net.Http.Json;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// BE#36 — Mərhələ 2 end to end, over the real HTTP API: self-service registration, approval, the
/// subscription's auto-block, recording payments, and the platform operator's console.
/// <para>
/// Everything a shop experiences is asserted through its own token, and everything the operator does is
/// asserted through the seeded <c>PlatformAdmin</c> token, so what these tests prove is exactly what a
/// customer and an operator would see.
/// </para>
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PlatformAdminApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    private HttpClient _admin = null!;

    public PlatformAdminApiTests(WarehouseApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseResetAsync();
        _admin = await _factory.PlatformAdminClientAsync();
    }

    public Task DisposeAsync()
    {
        _admin.Dispose();
        return Task.CompletedTask;
    }

    // --- Registration -----------------------------------------------------------------------------

    /// <summary>
    /// AC — register → the shop lands in <c>PendingApproval</c> and its owner is told to wait, not let in.
    /// </summary>
    [Fact]
    public async Task Registration_Creates_A_Pending_Shop_Whose_Owner_Cannot_Sign_In_Yet()
    {
        string phone = TenantPhones.Next();

        RegistrationDto registered = await RegisterAsync("Yeni Mağaza", phone);

        Assert.Equal(nameof(TenantStatus.PendingApproval), registered.Status);

        (HttpStatusCode status, ErrorDto error) = await LoginFailureAsync(phone);

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("Auth.TenantPendingApprovalForbidden", error.Code);
        Assert.Equal("Hesabınız təsdiq gözləyir", error.Message);
    }

    /// <summary>
    /// AC — the same phone may not register twice, anywhere on the platform. This is what closes the §4.1
    /// ambiguity: a phone that entered through registration identifies exactly one user.
    /// </summary>
    [Fact]
    public async Task Registering_An_Already_Used_Phone_Is_409()
    {
        string phone = TenantPhones.Next();
        await RegisterAsync("Birinci Mağaza", phone);

        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage second = await anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            storeName = "İkinci Mağaza",
            ownerName = "Başqa Sahibkar",
            phone,
            password = "demo123"
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var error = (await second.Content.ReadFromJsonAsync<ErrorDto>())!;
        Assert.Equal("Tenancy.PhoneAlreadyExists", error.Code);
    }

    /// <summary>A shop that already logs in (seeded demo owner) also blocks a registration on that phone.</summary>
    [Fact]
    public async Task Registration_Is_Refused_For_A_Phone_That_Already_Logs_In()
    {
        using HttpClient anonymous = _factory.CreateClient();

        HttpResponseMessage response = await anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            storeName = "Konflikt Mağaza",
            ownerName = "Sahibkar",
            phone = IntegrationTestHelpers.OwnerPhone,
            password = "demo123"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>Validation is a 400 with an Azerbaijani message, never a 500 from the database.</summary>
    [Fact]
    public async Task Registration_Rejects_An_Empty_Store_Name_And_A_Short_Password()
    {
        using HttpClient anonymous = _factory.CreateClient();

        HttpResponseMessage empty = await anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            storeName = "",
            ownerName = "Sahibkar",
            phone = TenantPhones.Next(),
            password = "demo123"
        });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        HttpResponseMessage shortPassword = await anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            storeName = "Mağaza",
            ownerName = "Sahibkar",
            phone = TenantPhones.Next(),
            password = "123"
        });
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);
    }

    // --- Approval ---------------------------------------------------------------------------------

    /// <summary>AC — approve → the owner signs in, and the shop now carries a paid period.</summary>
    [Fact]
    public async Task Approving_A_Pending_Shop_Lets_Its_Owner_Sign_In()
    {
        string phone = TenantPhones.Next();
        RegistrationDto registered = await RegisterAsync("Təsdiq Mağazası", phone);

        HttpResponseMessage approve = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{registered.TenantId}/approve", new { periodMonths = 3 });
        approve.EnsureSuccessStatusCode();

        var summary = (await approve.Content.ReadFromJsonAsync<TenantSummary>())!;
        Assert.Equal(nameof(TenantStatus.Active), summary.Status);
        Assert.NotNull(summary.ExpiresAt);
        Assert.False(summary.IsExpired);

        using HttpClient shop = await _factory.AuthenticatedClientAsync(phone, "demo123");
        Assert.Equal(HttpStatusCode.OK, (await shop.GetAsync("/api/products")).StatusCode);
    }

    /// <summary>Approving a shop that does not exist is a 404; a nonsensical period is a 400.</summary>
    [Fact]
    public async Task Approve_Validates_Its_Input_And_Its_Target()
    {
        HttpResponseMessage missing = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{Guid.NewGuid()}/approve", new { periodMonths = 1 });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("Validasiya Mağazası", TenantPhones.Next());

        foreach (int periodMonths in new[] { 0, -1, 1000 })
        {
            HttpResponseMessage bad = await _admin.PostAsJsonAsync(
                $"/api/admin/tenants/{shop.TenantId}/approve", new { periodMonths });
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        }
    }

    // --- Auto-block by expiry ---------------------------------------------------------------------

    /// <summary>
    /// AC — the headline rule. An <c>Active</c> shop whose period has lapsed is refused on <b>every</b>
    /// authenticated call (403 <c>SubscriptionExpired</c>) and cannot sign in again — and its status in the
    /// database is still <c>Active</c>, because the date is the verdict, not the status.
    /// </summary>
    [Fact]
    public async Task An_Expired_Subscription_Blocks_Every_Authenticated_Call()
    {
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("Vaxtı Bitmiş Mağaza", TenantPhones.Next());

        using HttpClient client = await _factory.AuthenticatedClientAsync(shop.OwnerPhone, shop.OwnerPassword);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/products")).StatusCode);

        await TenantTestFixture.SetExpiryAsync(shop.TenantId, DateTime.UtcNow.AddDays(-1));

        foreach (string route in new[] { "/api/products", "/api/customers", "/api/sales", "/api/auth/me" })
        {
            HttpResponseMessage response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            var error = (await response.Content.ReadFromJsonAsync<ErrorDto>())!;
            // BE#41 — the literal is spelled out rather than taken from WireFormat.ErrorCodes on purpose:
            // this is the string the frontend hard-codes, so the test has to fail if the constant moves.
            Assert.Equal("SubscriptionExpired", error.Code);
            Assert.Contains("Abunəliyiniz bitib", error.Message, StringComparison.Ordinal);
        }

        (HttpStatusCode status, ErrorDto loginError) = await LoginFailureAsync(shop.OwnerPhone, shop.OwnerPassword);
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("SubscriptionExpired", loginError.Code);

        // The shop was never demoted — expiry is enforced by the date alone.
        Tenant stored = await TenantTestFixture.GetTenantAsync(shop.TenantId);
        Assert.Equal(TenantStatus.Active, stored.Status);
    }

    /// <summary>
    /// AC — the refusal message carries the platform's contact number, and it comes from configuration
    /// (the factory's <c>PlatformAdmin:Phone</c>), never from a literal in the code.
    /// </summary>
    [Fact]
    public async Task The_Expiry_Message_Names_The_Configured_Support_Phone()
    {
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("Əlaqə Mağazası", TenantPhones.Next());
        using HttpClient client = await _factory.AuthenticatedClientAsync(shop.OwnerPhone, shop.OwnerPassword);

        await TenantTestFixture.SetExpiryAsync(shop.TenantId, DateTime.UtcNow.AddMinutes(-1));

        var perRequest = (await (await client.GetAsync("/api/products"))
            .Content.ReadFromJsonAsync<ErrorDto>())!;
        (_, ErrorDto atLogin) = await LoginFailureAsync(shop.OwnerPhone, shop.OwnerPassword);

        string expected = $"Abunəliyiniz bitib — əlaqə: {WarehouseApiFactory.PlatformAdminPhone}";
        Assert.Equal(expected, perRequest.Message);
        // Login and the per-request gate must say exactly the same thing — they are two implementations
        // of one contract.
        Assert.Equal(perRequest.Message, atLogin.Message);
    }

    /// <summary>
    /// AC — a shop with no end date is open-ended and can never be auto-blocked. This is what keeps the
    /// pre-existing installation ("İlk Mağaza", back-filled as NULL) working after the upgrade.
    /// </summary>
    [Fact]
    public async Task A_Shop_Without_An_Expiry_Is_Never_Auto_Blocked()
    {
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("Müddətsiz Mağaza", TenantPhones.Next());

        Tenant stored = await TenantTestFixture.GetTenantAsync(shop.TenantId);
        Assert.Null(stored.ExpiresAt);

        using HttpClient client = await _factory.AuthenticatedClientAsync(shop.OwnerPhone, shop.OwnerPassword);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/products")).StatusCode);

        // And the demo shop every other test uses is exactly this case.
        using HttpClient demo = await _factory.AuthenticatedClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await demo.GetAsync("/api/products")).StatusCode);
    }

    // --- Payments ---------------------------------------------------------------------------------

    /// <summary>AC — recording a payment re-opens a lapsed shop on its very next request.</summary>
    [Fact]
    public async Task Recording_A_Payment_Reopens_An_Expired_Shop()
    {
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("Ödəniş Mağazası", TenantPhones.Next());
        using HttpClient client = await _factory.AuthenticatedClientAsync(shop.OwnerPhone, shop.OwnerPassword);

        await TenantTestFixture.SetExpiryAsync(shop.TenantId, DateTime.UtcNow.AddDays(-3));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/products")).StatusCode);

        HttpResponseMessage payment = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{shop.TenantId}/payments",
            new { amount = 50m, periodMonths = 1, note = "Avqust ödənişi" });
        payment.EnsureSuccessStatusCode();

        // Same token, next request — no re-login needed, because nothing about the token changed.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/products")).StatusCode);

        List<PaymentDto> history = (await _admin.GetFromJsonAsync<List<PaymentDto>>(
            $"/api/admin/tenants/{shop.TenantId}/payments"))!;
        PaymentDto recorded = Assert.Single(history);
        Assert.Equal(50m, recorded.Amount);
        Assert.Equal(1, recorded.PeriodMonths);
        Assert.Equal("Avqust ödənişi", recorded.Note);
        Assert.NotNull(recorded.RecordedByAdminId);
    }

    /// <summary>
    /// AC — the extension arithmetic, over the API: a payment against a <b>live</b> period adds to the time
    /// still owned, while a payment against a <b>lapsed</b> one restarts from now.
    /// </summary>
    [Fact]
    public async Task Payment_Adds_To_A_Live_Period_And_Restarts_A_Lapsed_One()
    {
        // Live period: two months left, pay for one more → three months from the old expiry.
        TenantTestFixture.TenantHandle live =
            await TenantTestFixture.CreateTenantAsync("Erkən Ödəyən", TenantPhones.Next());
        DateTime remaining = DateTime.UtcNow.AddMonths(2);
        await TenantTestFixture.SetExpiryAsync(live.TenantId, remaining);

        await RecordPaymentAsync(live.TenantId, amount: 30m, periodMonths: 1);

        Tenant afterLive = await TenantTestFixture.GetTenantAsync(live.TenantId);
        AssertCloseTo(remaining.AddMonths(1), afterLive.ExpiresAt);

        // Lapsed period: the new month starts now, not on top of a date in the past.
        TenantTestFixture.TenantHandle lapsed =
            await TenantTestFixture.CreateTenantAsync("Gec Ödəyən", TenantPhones.Next());
        await TenantTestFixture.SetExpiryAsync(lapsed.TenantId, DateTime.UtcNow.AddMonths(-4));

        DateTime paidAt = DateTime.UtcNow;
        await RecordPaymentAsync(lapsed.TenantId, amount: 30m, periodMonths: 1);

        Tenant afterLapsed = await TenantTestFixture.GetTenantAsync(lapsed.TenantId);
        AssertCloseTo(paidAt.AddMonths(1), afterLapsed.ExpiresAt, TimeSpan.FromMinutes(5));
        Assert.False(afterLapsed.IsSubscriptionExpired(DateTime.UtcNow));
    }

    /// <summary>A payment must be real money for a real period, and against a real shop.</summary>
    [Fact]
    public async Task Payment_Validates_Amount_Period_And_Target()
    {
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("Validasiya Ödənişi", TenantPhones.Next());

        HttpResponseMessage zeroAmount = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{shop.TenantId}/payments", new { amount = 0m, periodMonths = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, zeroAmount.StatusCode);

        HttpResponseMessage negativeAmount = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{shop.TenantId}/payments", new { amount = -5m, periodMonths = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, negativeAmount.StatusCode);

        HttpResponseMessage badPeriod = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{shop.TenantId}/payments", new { amount = 10m, periodMonths = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, badPeriod.StatusCode);

        HttpResponseMessage unknownShop = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{Guid.NewGuid()}/payments", new { amount = 10m, periodMonths = 1 });
        Assert.Equal(HttpStatusCode.NotFound, unknownShop.StatusCode);

        HttpResponseMessage unknownHistory = await _admin.GetAsync($"/api/admin/tenants/{Guid.NewGuid()}/payments");
        Assert.Equal(HttpStatusCode.NotFound, unknownHistory.StatusCode);
    }

    // --- Admin console ----------------------------------------------------------------------------

    /// <summary>The list carries the billing summary and honours both filters.</summary>
    [Fact]
    public async Task Tenant_List_Filters_By_Status_And_Search_And_Shows_The_Billing_Summary()
    {
        string phone = TenantPhones.Next();
        RegistrationDto pending = await RegisterAsync("Axtarış Mağazası Qeyd", phone, ownerName: "Kamran Süleymanov");

        await _admin.PostAsJsonAsync($"/api/admin/tenants/{pending.TenantId}/approve", new { periodMonths = 2 });
        await RecordPaymentAsync(pending.TenantId, amount: 40m, periodMonths: 1);
        await RecordPaymentAsync(pending.TenantId, amount: 60m, periodMonths: 1);

        List<TenantRow> bySearch = (await _admin.GetFromJsonAsync<List<TenantRow>>(
            "/api/admin/tenants?search=Axtarış Mağazası"))!;

        TenantRow row = Assert.Single(bySearch);
        Assert.Equal(pending.TenantId, row.Id);
        Assert.Equal("Kamran Süleymanov", row.OwnerName);
        Assert.Equal(phone, row.Phone);
        Assert.Equal(nameof(TenantStatus.Active), row.Status);
        Assert.NotNull(row.ExpiresAt);
        Assert.False(row.IsExpired);
        Assert.Equal(100m, row.TotalPaid);
        Assert.Equal(60m, row.LastPaymentAmount);
        Assert.NotNull(row.LastPaymentAt);

        // The owner's name is searchable too.
        List<TenantRow> byOwner = (await _admin.GetFromJsonAsync<List<TenantRow>>(
            "/api/admin/tenants?search=Kamran Süleymanov"))!;
        Assert.Contains(byOwner, t => t.Id == pending.TenantId);

        // And the status filter excludes it once it is no longer pending.
        List<TenantRow> pendingOnly = (await _admin.GetFromJsonAsync<List<TenantRow>>(
            "/api/admin/tenants?status=PendingApproval"))!;
        Assert.DoesNotContain(pendingOnly, t => t.Id == pending.TenantId);
        Assert.All(pendingOnly, t => Assert.Equal(nameof(TenantStatus.PendingApproval), t.Status));

        HttpResponseMessage unknownStatus = await _admin.GetAsync("/api/admin/tenants?status=Nonsense");
        Assert.Equal(HttpStatusCode.BadRequest, unknownStatus.StatusCode);
    }

    /// <summary>
    /// TC-18 / BE#40 — the search is register-blind across all three searchable fields, <b>including</b>
    /// terms that carry a capital <c>I</c>.
    /// <para>
    /// That letter is the whole point of this test. The host culture is <c>az-Latn-AZ</c>, where
    /// <c>'I'.ToLower()</c> is <c>'ı'</c> (U+0131) while SQL Server's <c>LOWER()</c> produces <c>'i'</c>;
    /// the original implementation lowered the term in C# and the column in SQL, so an upper-case term
    /// silently matched nothing. Both terms below differ only in register and must return the same row —
    /// which they can only do if no culture-dependent casing is left on the path.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Tenant_Search_Ignores_Register_Even_For_Terms_Containing_A_Capital_I()
    {
        // Hex, so upper-casing it stays a valid term — and "Axtaris"/"Ismayilov" both carry the letter
        // that used to break: 'i' upper-cases to 'I', which az-Latn-AZ lower-cases back to 'ı'.
        string token = $"{Guid.NewGuid():N}"[..8];
        string storeName = $"QaAxtaris{token}Magaza";
        string ownerName = $"QaSahibkar{token}Ismayilov";
        string phone = TenantPhones.Next();

        await TenantTestFixture.CreateTenantAsync(storeName, phone, ownerName);

        // One term per searchable field: shop name, owner name, phone.
        foreach (string term in new[] { $"QaAxtaris{token}", $"QaSahibkar{token}", phone })
        {
            foreach (string register in new[] { term, term.ToUpperInvariant(), term.ToLowerInvariant() })
            {
                TenantRow row = Assert.Single(await SearchTenantsAsync(register));
                Assert.Equal(storeName, row.Name);
            }
        }

        // The pattern is escaped, so LIKE's own metacharacters are literal text: a lone '%' is a term
        // nobody is called, not a request for every shop on the platform.
        Assert.DoesNotContain(await SearchTenantsAsync("%"), t => t.Name == storeName);
        Assert.DoesNotContain(await SearchTenantsAsync("_"), t => t.Name == storeName);
    }

    /// <summary>
    /// BE#51 — since BE#46, <c>Phone</c> is stored canonically (<c>994501234567</c>). An admin typing the
    /// number the way a human actually would — local with a leading zero, with punctuation, with a leading
    /// <c>+</c>, or already canonical — must all find the same shop, because <c>PhoneNormalizer</c>
    /// canonicalizes the search term before it is compared, in addition to the raw <c>LIKE</c> fallback.
    /// </summary>
    [Fact]
    public async Task Tenant_Search_Finds_A_Phone_Typed_In_Any_Local_Spelling()
    {
        string canonicalPhone = TenantPhones.Next();
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("BE51 Telefon Axtarışı", canonicalPhone);

        // Strip the "994" country code to rebuild the local "0…" spelling the fixture normalized away.
        string localDigits = canonicalPhone[3..];
        string local = $"0{localDigits}";
        string localWithPunctuation =
            $"0{localDigits[..2]} {localDigits[2..5]}-{localDigits[5..7]}-{localDigits[7..]}";
        string withPlus = $"+{canonicalPhone}";

        foreach (string term in new[] { local, localWithPunctuation, withPlus, canonicalPhone })
        {
            TenantRow row = Assert.Single(await SearchTenantsAsync(term));
            Assert.Equal(shop.TenantId, row.Id);
            Assert.Equal(canonicalPhone, row.Phone);
        }
    }

    /// <summary>
    /// BE#51 — a term that cannot canonicalize into a full phone (wrong digit count) must keep falling back
    /// to the raw <c>LIKE</c>, so a deliberate partial search — the admin remembers only part of the number —
    /// still works exactly as it did before this fix.
    /// </summary>
    [Fact]
    public async Task Tenant_Search_By_A_Phone_Fragment_Still_Works()
    {
        string canonicalPhone = TenantPhones.Next();
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("BE51 Fraqment Axtarışı", canonicalPhone);

        // An interior slice, six digits long — neither the ten-digit local nor the twelve-digit canonical
        // length, so PhoneNormalizer refuses it and the raw LIKE fallback is what has to find the row.
        //
        // Assert.Contains rather than Assert.Single: TenantPhones hands out sequential, zero-padded numbers,
        // so with a low counter value this exact fragment can legitimately also appear inside other shops'
        // phones created earlier in the same run. What AC-3 promises is that the fragment still finds *this*
        // shop, not that it finds only this shop.
        string fragment = canonicalPhone.Substring(4, 6);

        List<TenantRow> rows = await SearchTenantsAsync(fragment);
        Assert.Contains(rows, r => r.Id == shop.TenantId);
    }

    /// <summary>The admin can create a shop that is usable immediately — no approval round trip.</summary>
    [Fact]
    public async Task Admin_Created_Shop_Is_Active_At_Once_And_Its_Owner_Can_Sign_In()
    {
        string phone = TenantPhones.Next();

        HttpResponseMessage created = await _admin.PostAsJsonAsync("/api/admin/tenants", new
        {
            storeName = "Admin Mağazası",
            ownerName = "Admin Sahibkarı",
            phone,
            password = "demo123",
            periodMonths = 6,
            monthlyFee = 25m
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var row = (await created.Content.ReadFromJsonAsync<TenantRow>())!;
        Assert.Equal(nameof(TenantStatus.Active), row.Status);
        Assert.NotNull(row.ExpiresAt);
        Assert.Equal(25m, row.MonthlyFee);
        Assert.Equal(0m, row.TotalPaid);

        using HttpClient shop = await _factory.AuthenticatedClientAsync(phone, "demo123");
        Assert.Equal(HttpStatusCode.OK, (await shop.GetAsync("/api/products")).StatusCode);

        // The shared phone rule applies to the admin's own form too.
        HttpResponseMessage duplicate = await _admin.PostAsJsonAsync("/api/admin/tenants", new
        {
            storeName = "Təkrar Mağaza",
            ownerName = "Sahibkar",
            phone,
            password = "demo123",
            periodMonths = (int?)null,
            monthlyFee = (decimal?)null
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    /// <summary>
    /// Block / unblock flip the status and leave the paid period exactly as it was — support decisions and
    /// billing are independent.
    /// </summary>
    [Fact]
    public async Task Block_And_Unblock_Do_Not_Touch_The_Subscription()
    {
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("Bloklanan Mağaza", TenantPhones.Next());
        DateTime expiry = DateTime.UtcNow.AddMonths(5);
        await TenantTestFixture.SetExpiryAsync(shop.TenantId, expiry);

        using HttpClient client = await _factory.AuthenticatedClientAsync(shop.OwnerPhone, shop.OwnerPassword);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/products")).StatusCode);

        HttpResponseMessage blocked = await _admin.PostAsync($"/api/admin/tenants/{shop.TenantId}/block", null);
        blocked.EnsureSuccessStatusCode();
        Assert.Equal(nameof(TenantStatus.Blocked),
            (await blocked.Content.ReadFromJsonAsync<TenantSummary>())!.Status);

        HttpResponseMessage refused = await client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("Auth.TenantBlockedForbidden",
            (await refused.Content.ReadFromJsonAsync<ErrorDto>())!.Code);

        HttpResponseMessage unblocked = await _admin.PostAsync($"/api/admin/tenants/{shop.TenantId}/unblock", null);
        unblocked.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/products")).StatusCode);

        Tenant stored = await TenantTestFixture.GetTenantAsync(shop.TenantId);
        AssertCloseTo(expiry, stored.ExpiresAt);

        Assert.Equal(HttpStatusCode.NotFound,
            (await _admin.PostAsync($"/api/admin/tenants/{Guid.NewGuid()}/block", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _admin.PostAsync($"/api/admin/tenants/{Guid.NewGuid()}/unblock", null)).StatusCode);
    }

    /// <summary>
    /// Stats move by exactly what the operator did. Asserted as deltas, because the suite shares one
    /// database and other tests are creating shops of their own.
    /// </summary>
    [Fact]
    public async Task Stats_Track_Shop_States_And_This_Months_Takings()
    {
        StatsDto before = (await _admin.GetFromJsonAsync<StatsDto>("/api/admin/stats"))!;

        RegistrationDto pending = await RegisterAsync("Statistika Mağazası", TenantPhones.Next());
        TenantTestFixture.TenantHandle active =
            await TenantTestFixture.CreateTenantAsync("Statistika Aktiv", TenantPhones.Next());
        TenantTestFixture.TenantHandle blocked = await TenantTestFixture.CreateTenantAsync(
            "Statistika Bloklu", TenantPhones.Next(), status: TenantStatus.Blocked);

        await RecordPaymentAsync(active.TenantId, amount: 75m, periodMonths: 1);

        StatsDto after = (await _admin.GetFromJsonAsync<StatsDto>("/api/admin/stats"))!;

        Assert.Equal(before.PendingCount + 1, after.PendingCount);
        Assert.Equal(before.ActiveCount + 1, after.ActiveCount);
        Assert.Equal(before.BlockedCount + 1, after.BlockedCount);
        Assert.Equal(before.CollectedThisMonth + 75m, after.CollectedThisMonth);

        // An expired-but-Active shop is counted separately, because it is locked out by its date rather
        // than by its status.
        await _admin.PostAsJsonAsync($"/api/admin/tenants/{pending.TenantId}/approve", new { periodMonths = 1 });
        await TenantTestFixture.SetExpiryAsync(pending.TenantId, DateTime.UtcNow.AddDays(-1));

        StatsDto withExpired = (await _admin.GetFromJsonAsync<StatsDto>("/api/admin/stats"))!;
        Assert.Equal(after.ExpiredCount + 1, withExpired.ExpiredCount);
    }

    // --- Authorization ----------------------------------------------------------------------------

    /// <summary>
    /// AC — an ordinary Sahibkar is the top role inside a shop and has no business on the platform console:
    /// every admin route answers 403, not 404 (the routes exist; the caller is simply not the operator).
    /// </summary>
    [Fact]
    public async Task An_Ordinary_Owner_Is_Forbidden_From_The_Whole_Admin_Surface()
    {
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("Adi Mağaza", TenantPhones.Next());
        using HttpClient owner = await _factory.AuthenticatedClientAsync(shop.OwnerPhone, shop.OwnerPassword);

        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/admin/tenants")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/admin/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await owner.GetAsync($"/api/admin/tenants/{shop.TenantId}/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await owner.PostAsync($"/api/admin/tenants/{shop.TenantId}/block", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(
            $"/api/admin/tenants/{shop.TenantId}/payments", new { amount = 1m, periodMonths = 1 })).StatusCode);
    }

    /// <summary>
    /// TC-5 — the policy is role-based, not just "not Owner": Manager and Seller (the demo shop's other two
    /// seeded roles) are refused exactly like Owner is, never treated as more or less trusted here.
    /// </summary>
    [Fact]
    public async Task Manager_And_Seller_Are_Also_Forbidden_From_The_Admin_Surface()
    {
        using HttpClient manager = await _factory.AuthenticatedClientAsync(
            IntegrationTestHelpers.ManagerPhone, IntegrationTestHelpers.DemoPassword);
        using HttpClient seller = await _factory.AuthenticatedClientAsync(
            IntegrationTestHelpers.SellerPhone, IntegrationTestHelpers.DemoPassword);

        foreach (HttpClient client in new[] { manager, seller })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/tenants")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/stats")).StatusCode);
        }
    }

    /// <summary>An anonymous caller gets 401 from the console, not a hint about what lives there.</summary>
    [Fact]
    public async Task The_Admin_Surface_Is_Closed_To_Anonymous_Callers()
    {
        using HttpClient anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/tenants")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/stats")).StatusCode);
    }

    // --- The platform admin's own identity ---------------------------------------------------------

    /// <summary>
    /// The design decision, pinned: a <c>PlatformAdmin</c> token passes the tenant gate (it belongs to no
    /// shop), yet reads <b>no</b> shop data. Its reserved tenant id owns no business row, so every
    /// tenant-scoped query comes back empty — fail-closed, not fail-open.
    /// </summary>
    [Fact]
    public async Task Platform_Admin_Passes_The_Tenant_Gate_But_Sees_No_Shop_Data()
    {
        // A shop with real data of its own, created first so there is definitely something to leak.
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("Məxfi Mağaza", TenantPhones.Next());
        using HttpClient owner = await _factory.AuthenticatedClientAsync(shop.OwnerPhone, shop.OwnerPassword);
        var product = await owner.CreateProductAsync($"ADM-{Guid.NewGuid():N}"[..20], quantity: 4);
        await owner.CreateCustomerAsync("Məxfi Müştəri");

        // The gate lets the admin through — not a 401 (no tenant claim) and not a 403 (unknown tenant).
        HttpResponseMessage products = await _admin.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, products.StatusCode);

        Assert.Empty((await products.Content.ReadFromJsonAsync<List<IntegrationTestHelpers.ProductDto>>())!);
        Assert.Empty((await _admin.GetFromJsonAsync<List<IntegrationTestHelpers.CustomerDto>>("/api/customers"))!);

        // Not even by id — the filter, not the route, is what refuses.
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.GetAsync($"/api/products/{product.Id}")).StatusCode);

        // The shop still sees its own rows: nothing above changed what the customer experiences.
        Assert.NotEmpty((await owner.GetFromJsonAsync<List<IntegrationTestHelpers.ProductDto>>("/api/products"))!);
    }

    /// <summary>
    /// The admin's own identity still resolves, which is the practical reason its user row carries a
    /// reserved tenant id instead of NULL or Guid.Empty: <c>GET /api/auth/me</c> works with no bypass.
    /// </summary>
    [Fact]
    public async Task Platform_Admin_Can_Read_Its_Own_Profile_And_Carries_The_Platform_Role()
    {
        var me = (await _admin.GetFromJsonAsync<MeDto>("/api/auth/me"))!;

        Assert.Equal(WarehouseApiFactory.PlatformAdminCanonicalPhone, me.Phone);
        Assert.Equal("platform_admin", me.Role);
        Assert.Equal(WarehouseApiFactory.PlatformAdminName, me.FullName);
    }

    /// <summary>
    /// Seeding is idempotent: the host has been started (and migrated) repeatedly across the suite, and
    /// there is still exactly one platform admin, with the configured phone.
    /// </summary>
    [Fact]
    public async Task The_Platform_Admin_Is_Seeded_Exactly_Once()
    {
        using HttpClient again = await _factory.PlatformAdminClientAsync();
        var me = (await again.GetFromJsonAsync<MeDto>("/api/auth/me"))!;

        Assert.Equal(WarehouseApiFactory.PlatformAdminCanonicalPhone, me.Phone);
    }

    // --- Helpers ----------------------------------------------------------------------------------

    private async Task<RegistrationDto> RegisterAsync(
        string storeName,
        string phone,
        string ownerName = "Test Sahibkar",
        string password = "demo123")
    {
        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage response = await anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            storeName,
            ownerName,
            phone,
            password
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegistrationDto>())!;
    }

    private async Task<List<TenantRow>> SearchTenantsAsync(string term) =>
        (await _admin.GetFromJsonAsync<List<TenantRow>>(
            $"/api/admin/tenants?search={Uri.EscapeDataString(term)}"))!;

    private async Task RecordPaymentAsync(Guid tenantId, decimal amount, int periodMonths, string? note = null)
    {
        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{tenantId}/payments", new { amount, periodMonths, note });
        response.EnsureSuccessStatusCode();
    }

    private async Task<(HttpStatusCode Status, ErrorDto Error)> LoginFailureAsync(
        string phone,
        string password = "demo123")
    {
        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage response = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { phone, password });

        return (response.StatusCode, (await response.Content.ReadFromJsonAsync<ErrorDto>())!);
    }

    /// <summary>
    /// The expiry is computed from the server's clock, so it can differ from the test's by a few
    /// milliseconds. Everything asserted here is about months, not milliseconds.
    /// </summary>
    private static void AssertCloseTo(DateTime expected, DateTime? actual, TimeSpan? tolerance = null)
    {
        Assert.NotNull(actual);
        Assert.True(
            (actual!.Value - expected).Duration() <= (tolerance ?? TimeSpan.FromMinutes(1)),
            $"Gözlənilən {expected:O}, gələn {actual:O}");
    }

    private sealed record RegistrationDto(Guid TenantId, string StoreName, string Status, string Message);

    private sealed record TenantSummary(
        Guid Id, string Name, string Status, DateTime? ExpiresAt, decimal MonthlyFee, bool IsExpired);

    private sealed record TenantRow(
        Guid Id,
        string Name,
        string? OwnerName,
        string? Phone,
        string Status,
        DateTime? ExpiresAt,
        decimal MonthlyFee,
        bool IsExpired,
        DateTime? LastPaymentAt,
        decimal? LastPaymentAmount,
        decimal TotalPaid);

    private sealed record PaymentDto(
        Guid Id,
        Guid TenantId,
        decimal Amount,
        DateTime PaidAt,
        int PeriodMonths,
        string? Note,
        Guid? RecordedByAdminId);

    private sealed record StatsDto(
        int ActiveCount, int PendingCount, int BlockedCount, int ExpiredCount, decimal CollectedThisMonth);

    private sealed record MeDto(Guid Id, string FullName, string Phone, string Role);

    private sealed record ErrorDto(string Code, string Message);
}
