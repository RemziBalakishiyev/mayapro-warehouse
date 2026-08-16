using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MayaPro.WarehouseApi.Modules.Tenancy.Application;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// QA (BE#36) — the test cases from the PM's TC list that the delivered suite does not assert directly:
/// TC-12, TC-15 (negative period), TC-16, TC-17 and TC-21, plus the platform admin's read isolation on
/// <c>/api/sales</c>. Added by the QA agent; nothing under <c>src/</c> was changed.
/// <para>
/// TC-18 lives with its fix rather than here: the case-insensitive search was <b>broken</b> for terms
/// containing an upper-case <c>I</c> (<c>docs/qa/BE36-qa-report.md</c>, BUG-1 → BE#40), and the regression
/// test landed in the same change as the correction —
/// <see cref="PlatformAdminApiTests.Tenant_Search_Ignores_Register_Even_For_Terms_Containing_A_Capital_I"/>.
/// </para>
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class Be36QaGapTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;
    private HttpClient _admin = null!;

    public Be36QaGapTests(WarehouseApiFactory factory) => _factory = factory;

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

    /// <summary>
    /// TC-21 — the owner user cannot be created, so the shop must not survive either. The provisioning
    /// service is rebuilt inside the host's own scope with a failing <see cref="IIdentityProvisioning"/>,
    /// which is the only way to make the second write fail on demand: everything the endpoint itself
    /// rejects is rejected before the transaction opens.
    /// </summary>
    [Fact]
    public async Task Tc21_A_Failed_Owner_Creation_Leaves_No_Orphan_Tenant()
    {
        string storeName = $"QA Yetim Mağaza {Guid.NewGuid():N}";

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var provisioning = new TenantProvisioning(db, new ExplodingIdentityProvisioning(), unitOfWork);

            await Assert.ThrowsAsync<InvalidOperationException>(() => provisioning.ProvisionAsync(
                storeName,
                "QA Sahibkar",
                TenantPhones.Next(),
                "demo123",
                TenantStatus.PendingApproval,
                DateTime.UtcNow,
                months: null,
                monthlyFee: 0m,
                CancellationToken.None));
        }

        // Read on a fresh connection: what matters is whether the row was committed, not what the failed
        // scope's change tracker still remembers.
        await using var verify = new TenancyDbContext(new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlServer(WarehouseApiFactory.TestConnectionString).Options);

        Assert.False(await verify.Tenants.AnyAsync(t => t.Name == storeName));
    }

    /// <summary>
    /// TC-12 — a blocked shop's owner is refused at login with the support number that comes from
    /// configuration, and gets no token.
    /// </summary>
    [Fact]
    public async Task Tc12_A_Blocked_Shop_Is_Refused_At_Login_With_The_Configured_Phone()
    {
        TenantTestFixture.TenantHandle shop = await TenantTestFixture.CreateTenantAsync(
            "QA Bloklu Mağaza", TenantPhones.Next(), status: TenantStatus.Blocked);

        using HttpClient anonymous = _factory.CreateClient();
        HttpResponseMessage response = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { phone = shop.OwnerPhone, password = shop.OwnerPassword });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        string raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", raw, StringComparison.OrdinalIgnoreCase);

        var error = JsonSerializer.Deserialize<ErrorDto>(
            raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal("Auth.TenantBlockedForbidden", error.Code);
        Assert.Equal($"Abunəliyiniz bitib — əlaqə: {WarehouseApiFactory.PlatformAdminPhone}", error.Message);
    }

    /// <summary>TC-15 — a negative period is refused just like zero, and nothing is written.</summary>
    [Fact]
    public async Task Tc15_A_Negative_Period_Is_Rejected_And_Writes_Nothing()
    {
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("QA Mənfi Ay", TenantPhones.Next());
        DateTime expiry = DateTime.UtcNow.AddMonths(3);
        await TenantTestFixture.SetExpiryAsync(shop.TenantId, expiry);

        HttpResponseMessage negativePayment = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{shop.TenantId}/payments", new { amount = 10m, periodMonths = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, negativePayment.StatusCode);

        HttpResponseMessage negativeApprove = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{shop.TenantId}/approve", new { periodMonths = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, negativeApprove.StatusCode);

        Assert.Empty((await _admin.GetFromJsonAsync<List<PaymentDto>>(
            $"/api/admin/tenants/{shop.TenantId}/payments"))!);

        Tenant stored = await TenantTestFixture.GetTenantAsync(shop.TenantId);
        Assert.NotNull(stored.ExpiresAt);
        Assert.True((stored.ExpiresAt!.Value - expiry).Duration() <= TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// TC-16 — two shops with payments of their own: the history endpoint returns only the shop asked
    /// for, newest first.
    /// </summary>
    [Fact]
    public async Task Tc16_Payment_History_Is_Per_Shop_And_Newest_First()
    {
        TenantTestFixture.TenantHandle a =
            await TenantTestFixture.CreateTenantAsync("QA Tarixçə A", TenantPhones.Next());
        TenantTestFixture.TenantHandle b =
            await TenantTestFixture.CreateTenantAsync("QA Tarixçə B", TenantPhones.Next());

        await PayAsync(a.TenantId, 11m, 1, "A-1");
        await PayAsync(b.TenantId, 99m, 1, "B-1");
        await PayAsync(a.TenantId, 22m, 2, "A-2");
        await PayAsync(b.TenantId, 98m, 1, "B-2");

        List<PaymentDto> historyA =
            (await _admin.GetFromJsonAsync<List<PaymentDto>>($"/api/admin/tenants/{a.TenantId}/payments"))!;

        Assert.Equal(2, historyA.Count);
        Assert.All(historyA, p => Assert.Equal(a.TenantId, p.TenantId));
        Assert.DoesNotContain(historyA, p => p.Note is "B-1" or "B-2");
        Assert.Equal(new[] { "A-2", "A-1" }, historyA.Select(p => p.Note).ToArray());
        Assert.True(historyA[0].PaidAt >= historyA[1].PaidAt);
    }

    /// <summary>
    /// TC-17 — <c>collectedThisMonth</c> counts only the current calendar month. A payment dated last
    /// month is written straight to the table (the API always stamps "now"), and the figure must not move.
    /// Asserted as a delta: the suite shares one database.
    /// </summary>
    [Fact]
    public async Task Tc17_This_Months_Takings_Exclude_A_Payment_From_Last_Month()
    {
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("QA Bu Ay", TenantPhones.Next());

        StatsDto before = (await _admin.GetFromJsonAsync<StatsDto>("/api/admin/stats"))!;

        await PayAsync(shop.TenantId, 100m, 1, "bu ay 1");
        await PayAsync(shop.TenantId, 150m, 1, "bu ay 2");

        await using (var db = new TenancyDbContext(new DbContextOptionsBuilder<TenancyDbContext>()
                         .UseSqlServer(WarehouseApiFactory.TestConnectionString).Options))
        {
            db.SubscriptionPayments.Add(SubscriptionPayment.Create(
                shop.TenantId, 200m, DateTime.UtcNow.AddMonths(-1), 1, "keçən ay", null));
            await db.SaveChangesAsync();
        }

        StatsDto after = (await _admin.GetFromJsonAsync<StatsDto>("/api/admin/stats"))!;

        Assert.Equal(before.CollectedThisMonth + 250m, after.CollectedThisMonth);
    }

    /// <summary>
    /// Security — the delivered suite proves the platform admin reads no products and no customers;
    /// <c>/api/sales</c> is the third tenant-scoped surface and is checked here too.
    /// </summary>
    [Fact]
    public async Task Platform_Admin_Reads_No_Sales_Either()
    {
        TenantTestFixture.TenantHandle shop =
            await TenantTestFixture.CreateTenantAsync("QA Satış Mağazası", TenantPhones.Next());
        using HttpClient owner = await _factory.AuthenticatedClientAsync(shop.OwnerPhone, shop.OwnerPassword);

        var product = await owner.CreateProductAsync($"QAS-{Guid.NewGuid():N}"[..20], quantity: 5);
        HttpResponseMessage sold = await owner.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 1,
            totalAmount = 10m,
            paymentType = WireFormat.PaymentTypes.Cash,
            customerId = (Guid?)null
        });
        sold.EnsureSuccessStatusCode();

        var ownersSales = (await owner.GetFromJsonAsync<IntegrationTestHelpers.PagedSalesDto>("/api/sales"))!;
        Assert.NotEmpty(ownersSales.Items);

        var adminsSales = (await _admin.GetFromJsonAsync<IntegrationTestHelpers.PagedSalesDto>("/api/sales"))!;
        Assert.Empty(adminsSales.Items);
        Assert.Equal(0, adminsSales.Total);
    }

    private async Task PayAsync(Guid tenantId, decimal amount, int periodMonths, string note)
    {
        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            $"/api/admin/tenants/{tenantId}/payments", new { amount, periodMonths, note });
        response.EnsureSuccessStatusCode();

        // The ordering assertion needs distinguishable timestamps.
        await Task.Delay(15);
    }

    /// <summary>Fails the way a DB error would: after the tenant row is already in the transaction.</summary>
    private sealed class ExplodingIdentityProvisioning : IIdentityProvisioning
    {
        public Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<Guid> AddOwnerAsync(
            Guid tenantId, string fullName, string phone, string password,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("QA: istifadəçi yaradıla bilmədi");
    }

    private sealed record PaymentDto(
        Guid Id, Guid TenantId, decimal Amount, DateTime PaidAt, int PeriodMonths, string? Note,
        Guid? RecordedByAdminId);

    private sealed record StatsDto(
        int ActiveCount, int PendingCount, int BlockedCount, int ExpiredCount, decimal CollectedThisMonth);

    private sealed record ErrorDto(string Code, string Message);
}
