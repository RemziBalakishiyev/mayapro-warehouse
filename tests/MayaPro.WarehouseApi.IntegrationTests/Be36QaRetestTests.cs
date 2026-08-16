using System.Net;
using System.Net.Http.Json;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// QA retest (BE#36, cycle 2) — independent evidence that BE#40, BE#41 and BE#42 are actually closed, plus
/// the adversarial cases the fixes newly opened. Written by the QA agent; nothing under <c>src/</c> was
/// touched.
/// <para>
/// These tests deliberately do not reuse the constants the production code exposes: every contract string
/// (<c>SubscriptionExpired</c>, <c>periodMonths</c>, <c>collectedThisMonth</c>) is spelled out literally, so
/// renaming a constant cannot make the suite agree with the rename.
/// </para>
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class Be36QaRetestTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;
    private HttpClient _admin = null!;

    public Be36QaRetestTests(WarehouseApiFactory factory) => _factory = factory;

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

    // ------------------------------------------------------------------ BE#40

    /// <summary>
    /// BE#40 / AC-14 / TC-18 — the exact repro from the first QA cycle, re-run verbatim: the very terms that
    /// used to return an empty list (<c>QAAXTARIS…</c>, <c>QASAHIBKAR…</c>) must now return the same single
    /// row as their lower-case twins, across all three searchable fields.
    /// </summary>
    [Fact]
    public async Task Be40_Search_Ignores_Register_On_Name_Owner_And_Phone_Including_Capital_I()
    {
        string token = $"{Guid.NewGuid():N}"[..8];
        string storeName = $"QaAxtaris{token}Magaza";
        string ownerName = $"QaSahibkar{token}Adi";
        string phone = TenantPhones.Next();

        await TenantTestFixture.CreateTenantAsync(storeName, phone, ownerName);

        foreach (string term in new[] { $"QaAxtaris{token}", $"QaSahibkar{token}", phone })
        {
            // The upper-case register is the one that used to fail: 'I' lower-cases to 'ı' (U+0131) under
            // az-Latn-AZ but to 'i' in SQL Server, so the two sides could never meet.
            foreach (string register in new[] { term, term.ToUpperInvariant(), term.ToLowerInvariant() })
            {
                List<TenantRow> rows = await SearchAsync(register);
                Assert.Single(rows);
                Assert.Equal(storeName, rows[0].Name);
            }
        }
    }

    /// <summary>
    /// BE#40 (new risk) — the term now goes into <c>LIKE</c>, so its metacharacters must be neutralised.
    /// Both directions are asserted: a shop whose name really contains <c>%</c> or <c>_</c> is still found by
    /// typing them, and none of those characters may act as a wildcard against a shop that does not.
    /// </summary>
    [Fact]
    public async Task Be40_Like_Metacharacters_Are_Literal_Text_Not_Wildcards()
    {
        string token = $"{Guid.NewGuid():N}"[..8];

        // A plain shop that must never be dragged in by a wildcard-ish term.
        string plainName = $"QaEscape{token}Magaza";
        await TenantTestFixture.CreateTenantAsync(plainName, TenantPhones.Next());

        // A shop whose name really does contain '%' and '_' — the literal-match control.
        string literalName = $"QaLike{token}50%Endirim_A";
        await TenantTestFixture.CreateTenantAsync(literalName, TenantPhones.Next());

        // '%' alone: only rows that literally contain a percent sign, never the whole platform.
        List<TenantRow> percent = await SearchAsync("%");
        Assert.DoesNotContain(percent, r => r.Name == plainName);
        Assert.Contains(percent, r => r.Name == literalName);

        // '_' alone: same reasoning — it must not match every single-character position.
        List<TenantRow> underscore = await SearchAsync("_");
        Assert.DoesNotContain(underscore, r => r.Name == plainName);
        Assert.Contains(underscore, r => r.Name == literalName);

        // Literal direction: a '%' and a '_' typed inside a real term still find the row that has them.
        // (This is what the URL-encoded 50%25 carries.)
        List<TenantRow> fiftyPercent = await SearchAsync($"QaLike{token}50%");
        Assert.Single(fiftyPercent);
        Assert.Equal(literalName, fiftyPercent[0].Name);
        Assert.Single(await SearchAsync($"{token}50%Endirim_A"));

        // Wildcard direction — the assertions that would fail if the escaping were dropped:
        //   '_' standing in for the 'a' of "Magaza",
        //   '%' standing in for "anything at all",
        //   '[Qq]' behaving like a character class.
        Assert.Empty(await SearchAsync($"QaEscape{token}M_gaza"));
        Assert.Empty(await SearchAsync($"QaEscape{token}%Magaza"));
        Assert.Empty(await SearchAsync($"[Qq]aEscape{token}"));

        // Above all: no hostile term may reach SQL malformed. A 5xx here is the bug this guards against.
        foreach (string hostile in new[] { "%", "_", "[", "]", "\\", "\\%", "50%", "%_[\\", "[]", "%%%", "___" })
        {
            HttpResponseMessage response = await _admin.GetAsync(
                $"/api/admin/tenants?search={Uri.EscapeDataString(hostile)}");

            Assert.True((int)response.StatusCode < 500,
                $"search={hostile} answered {(int)response.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            List<TenantRow> rows = (await response.Content.ReadFromJsonAsync<List<TenantRow>>())!;
            Assert.DoesNotContain(rows, r => r.Name == plainName);
        }
    }

    /// <summary>
    /// BE#40 (new risk) — degenerate terms: an empty and a whitespace-only <c>search</c> mean "no filter"
    /// (they must not silently hide every shop), and a 1000-character term is answered, not blown up.
    /// </summary>
    [Fact]
    public async Task Be40_Empty_Blank_And_Overlong_Search_Terms_Are_Handled()
    {
        string token = $"{Guid.NewGuid():N}"[..8];
        string storeName = $"QaBos{token}Magaza";
        await TenantTestFixture.CreateTenantAsync(storeName, TenantPhones.Next());

        foreach (string blank in new[] { "", "   ", "\t" })
        {
            HttpResponseMessage response = await _admin.GetAsync(
                $"/api/admin/tenants?search={Uri.EscapeDataString(blank)}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            List<TenantRow> rows = (await response.Content.ReadFromJsonAsync<List<TenantRow>>())!;
            Assert.Contains(rows, r => r.Name == storeName);
        }

        foreach (string overlong in new[] { new string('a', 1000), new string('%', 1000), new string('\\', 999) })
        {
            HttpResponseMessage response = await _admin.GetAsync(
                $"/api/admin/tenants?search={Uri.EscapeDataString(overlong)}");

            Assert.True((int)response.StatusCode < 500,
                $"a {overlong.Length}-character term answered {(int)response.StatusCode}");

            // A term nobody is called matches nobody; a 400 would be an acceptable contract too, but the
            // endpoint declares no length limit, so OK + empty is what it must answer.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty((await response.Content.ReadFromJsonAsync<List<TenantRow>>())!);
        }
    }

    // ------------------------------------------------------------------ BE#41

    /// <summary>
    /// BE#41 / AC-16 — an expired subscription answers <b>403</b> with the code spelled exactly
    /// <c>SubscriptionExpired</c> on <b>both</b> paths: an existing token mid-session, and a fresh login.
    /// The login path is the one the fix put at risk — the code carries no <c>…Forbidden</c> suffix, so
    /// without an explicit mapping the suffix convention would have answered 400.
    /// </summary>
    [Fact]
    public async Task Be41_Expired_Subscription_Is_403_SubscriptionExpired_At_Login_And_Mid_Session()
    {
        TenantTestFixture.TenantHandle shop = await TenantTestFixture.CreateTenantAsync(
            $"QA Bitmis Abune {Guid.NewGuid():N}"[..40], TenantPhones.Next());

        // Signed in while the shop was still open — the token outlives the subscription.
        using HttpClient owner = await _factory.AuthenticatedClientAsync(shop.OwnerPhone, shop.OwnerPassword);
        (await owner.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();

        await TenantTestFixture.SetExpiryAsync(shop.TenantId, DateTime.UtcNow.AddDays(-1));

        // Mid-session: every authenticated endpoint.
        foreach (string path in new[] { "/api/auth/me", "/api/products", "/api/customers", "/api/sales" })
        {
            HttpResponseMessage response = await owner.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            await AssertSubscriptionExpiredBodyAsync(response);
        }

        // Login: same status, same code — not 400, not "Auth."-prefixed.
        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage login = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { phone = shop.OwnerPhone, password = shop.OwnerPassword });

        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);

        string rawLogin = await login.Content.ReadAsStringAsync();
        Assert.Contains("\"code\":\"SubscriptionExpired\"", rawLogin, StringComparison.Ordinal);
        Assert.DoesNotContain("token", rawLogin, StringComparison.OrdinalIgnoreCase);

        // The shop was never demoted: the date is the verdict.
        Tenant stored = await TenantTestFixture.GetTenantAsync(shop.TenantId);
        Assert.Equal(TenantStatus.Active, stored.Status);
    }

    private static async Task AssertSubscriptionExpiredBodyAsync(HttpResponseMessage response)
    {
        var error = (await response.Content.ReadFromJsonAsync<ErrorDto>())!;

        Assert.Equal("SubscriptionExpired", error.Code);
        Assert.DoesNotContain("Auth.", error.Code, StringComparison.Ordinal);
        Assert.Contains("Abunəliyiniz bitib", error.Message, StringComparison.Ordinal);
        Assert.Contains(WarehouseApiFactory.PlatformAdminPhone, error.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ BE#42

    /// <summary>
    /// BE#42 / AC-14 — the payment body is <c>periodMonths</c>: it binds, the row is written and the period
    /// moves by exactly one month. The retired name is proven gone rather than merely unused: sending
    /// <c>months</c> leaves the field unbound and is refused with 400.
    /// </summary>
    [Fact]
    public async Task Be42_Payment_Accepts_PeriodMonths_And_Rejects_The_Retired_Months_Name()
    {
        TenantTestFixture.TenantHandle shop = await TenantTestFixture.CreateTenantAsync(
            $"QA Odenis {Guid.NewGuid():N}"[..30], TenantPhones.Next());

        DateTime expiry = DateTime.UtcNow.AddMonths(2);
        await TenantTestFixture.SetExpiryAsync(shop.TenantId, expiry);

        HttpResponseMessage paid = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{shop.TenantId}/payments", new { amount = 50m, periodMonths = 1 });

        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);

        List<PaymentDto> history = (await _admin.GetFromJsonAsync<List<PaymentDto>>(
            $"/api/admin/tenants/{shop.TenantId}/payments"))!;

        PaymentDto row = Assert.Single(history);
        Assert.Equal(50m, row.Amount);
        Assert.Equal(1, row.PeriodMonths);

        Tenant stored = await TenantTestFixture.GetTenantAsync(shop.TenantId);
        Assert.NotNull(stored.ExpiresAt);
        Assert.True((stored.ExpiresAt!.Value - expiry.AddMonths(1)).Duration() <= TimeSpan.FromMinutes(5),
            $"ExpiresAt {stored.ExpiresAt} is not one month past {expiry}");

        // The old name no longer binds anywhere, and no alias quietly accepts it.
        HttpResponseMessage legacyPayment = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{shop.TenantId}/payments", new { amount = 50m, months = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, legacyPayment.StatusCode);

        HttpResponseMessage legacyApprove = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{shop.TenantId}/approve", new { months = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, legacyApprove.StatusCode);

        // Nothing was written by the refused calls.
        Assert.Single((await _admin.GetFromJsonAsync<List<PaymentDto>>(
            $"/api/admin/tenants/{shop.TenantId}/payments"))!);
    }

    /// <summary>
    /// BE#42 / AC-14 — <c>POST /api/admin/tenants</c> reads <c>periodMonths</c> too. Sent under the retired
    /// name the period is simply absent (the shop is open-ended), which is how an unbound optional field
    /// must behave — and proves no alias survives.
    /// </summary>
    [Fact]
    public async Task Be42_Create_Tenant_Reads_PeriodMonths_And_Ignores_The_Retired_Name()
    {
        HttpResponseMessage created = await _admin.PostAsJsonAsync("/api/admin/tenants", new
        {
            storeName = $"QA Yaradilan {Guid.NewGuid():N}"[..30],
            ownerName = "QA Sahibkar",
            phone = TenantPhones.Next(),
            password = "demo123",
            periodMonths = 6,
            monthlyFee = 25m
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var summary = (await created.Content.ReadFromJsonAsync<TenantRow>())!;
        Tenant withPeriod = await TenantTestFixture.GetTenantAsync(summary.Id);
        Assert.NotNull(withPeriod.ExpiresAt);
        Assert.True(
            (withPeriod.ExpiresAt!.Value - DateTime.UtcNow.AddMonths(6)).Duration() <= TimeSpan.FromMinutes(5));

        HttpResponseMessage legacy = await _admin.PostAsJsonAsync("/api/admin/tenants", new
        {
            storeName = $"QA Kohne Ad {Guid.NewGuid():N}"[..30],
            ownerName = "QA Sahibkar",
            phone = TenantPhones.Next(),
            password = "demo123",
            months = 6,
            monthlyFee = 25m
        });

        Assert.Equal(HttpStatusCode.Created, legacy.StatusCode);

        var legacySummary = (await legacy.Content.ReadFromJsonAsync<TenantRow>())!;
        Tenant openEnded = await TenantTestFixture.GetTenantAsync(legacySummary.Id);
        Assert.Null(openEnded.ExpiresAt);
    }

    /// <summary>
    /// BE#42 / AC-14 — <c>GET /api/admin/stats</c> answers <c>collectedThisMonth</c>. Asserted on the raw
    /// JSON, because a DTO with the right property name would deserialise happily from either spelling.
    /// </summary>
    [Fact]
    public async Task Be42_Stats_Field_Is_Named_CollectedThisMonth_On_The_Wire()
    {
        string raw = await _admin.GetStringAsync("/api/admin/stats");

        Assert.Contains("\"collectedThisMonth\"", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("thisMonthCollected", raw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The whole subscription arc on the corrected contract, end to end and in one place:
    /// register → 201 → login 403 (pending) → approve(periodMonths) → login 200 → expiry → 403
    /// SubscriptionExpired → payment(periodMonths) → 200 on the same token.
    /// </summary>
    [Fact]
    public async Task Be36_Full_Subscription_Arc_On_The_Corrected_Contract()
    {
        string phone = TenantPhones.Next();
        string password = "demo123";

        using HttpClient anonymous = _factory.CreateClient();

        HttpResponseMessage registered = await anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            storeName = $"QA Axin {Guid.NewGuid():N}"[..30],
            ownerName = "QA Axin Sahibkari",
            phone,
            password
        });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        var shop = (await registered.Content.ReadFromJsonAsync<RegistrationRow>())!;

        // Pending → refused, with the pending code (not the subscription one).
        HttpResponseMessage pendingLogin = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { phone, password });
        Assert.Equal(HttpStatusCode.Forbidden, pendingLogin.StatusCode);
        Assert.Equal("Auth.TenantPendingApprovalForbidden",
            (await pendingLogin.Content.ReadFromJsonAsync<ErrorDto>())!.Code);

        HttpResponseMessage approved = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{shop.TenantId}/approve", new { periodMonths = 1 });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        using HttpClient owner = await _factory.AuthenticatedClientAsync(phone, password);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/products")).StatusCode);

        await TenantTestFixture.SetExpiryAsync(shop.TenantId, DateTime.UtcNow.AddDays(-1));

        HttpResponseMessage locked = await owner.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
        Assert.Equal("SubscriptionExpired", (await locked.Content.ReadFromJsonAsync<ErrorDto>())!.Code);

        HttpResponseMessage reopened = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{shop.TenantId}/payments",
            new { amount = 50m, periodMonths = 1, note = "QA retest" });
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);

        // Same token, no re-login.
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/products")).StatusCode);
    }

    private async Task<List<TenantRow>> SearchAsync(string term) =>
        (await _admin.GetFromJsonAsync<List<TenantRow>>(
            $"/api/admin/tenants?search={Uri.EscapeDataString(term)}"))!;

    private sealed record TenantRow(Guid Id, string Name, string? OwnerName, string? Phone, string Status);

    private sealed record RegistrationRow(Guid TenantId, string StoreName, string Status, string Message);

    private sealed record PaymentDto(
        Guid Id, Guid TenantId, decimal Amount, DateTime PaidAt, int PeriodMonths, string? Note,
        Guid? RecordedByAdminId);

    private sealed record ErrorDto(string Code, string Message);
}
