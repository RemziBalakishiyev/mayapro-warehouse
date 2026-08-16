using MayaPro.WarehouseApi.Modules.Tenancy.Domain;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Tests;

/// <summary>
/// BE#36 — the subscription arithmetic, pinned down without a database or a clock. Every rule below takes
/// "now" as an argument precisely so it can be tested at an exact instant; the handlers pass
/// <c>IDateProvider.UtcNow</c>, never <c>DateTime.UtcNow</c> directly.
/// </summary>
public sealed class SubscriptionPeriodTests
{
    private static readonly DateTime Now = new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>The headline rule: still-running periods are added to, never thrown away.</summary>
    [Fact]
    public void Extending_A_Live_Period_Adds_To_The_Remaining_Time()
    {
        Tenant tenant = ActiveTenant();
        tenant.Approve(Now, months: 3);              // expires 2026-11-16
        DateTime beforePayment = tenant.ExpiresAt!.Value;

        tenant.Extend(Now, months: 2);

        Assert.Equal(beforePayment.AddMonths(2), tenant.ExpiresAt);
        Assert.Equal(new DateTime(2027, 1, 16, 10, 0, 0, DateTimeKind.Utc), tenant.ExpiresAt);
    }

    /// <summary>The other half: a lapsed period restarts from now, not from the date in the past.</summary>
    [Fact]
    public void Extending_A_Lapsed_Period_Restarts_From_Now()
    {
        Tenant tenant = ActiveTenant();
        tenant.Approve(Now.AddMonths(-6), months: 1); // expired five months ago
        Assert.True(tenant.IsSubscriptionExpired(Now));

        tenant.Extend(Now, months: 2);

        Assert.Equal(Now.AddMonths(2), tenant.ExpiresAt);
        Assert.False(tenant.IsSubscriptionExpired(Now));
    }

    /// <summary>An open-ended shop that starts paying gets a period beginning now.</summary>
    [Fact]
    public void Extending_An_Open_Ended_Shop_Starts_The_Period_Now()
    {
        Tenant tenant = ActiveTenant();
        Assert.Null(tenant.ExpiresAt);

        tenant.Extend(Now, months: 1);

        Assert.Equal(Now.AddMonths(1), tenant.ExpiresAt);
    }

    /// <summary>Approval is a fresh start: it assigns the period rather than extending a stale one.</summary>
    [Fact]
    public void Approving_Activates_And_Starts_The_Period_At_Now()
    {
        Tenant tenant = Tenant.Create("Yeni mağaza", "Sahibkar", "0700000001");
        Assert.Equal(TenantStatus.PendingApproval, tenant.Status);

        tenant.Approve(Now, months: 12);

        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(Now.AddMonths(12), tenant.ExpiresAt);
    }

    /// <summary>
    /// An approval that lands on a shop whose old period is still running does <b>not</b> keep the old date —
    /// this is the deliberate difference from <see cref="Tenant.Extend"/>.
    /// </summary>
    [Fact]
    public void Approving_Overwrites_A_Still_Running_Period()
    {
        Tenant tenant = ActiveTenant();
        tenant.Approve(Now, months: 24);

        tenant.Approve(Now, months: 1);

        Assert.Equal(Now.AddMonths(1), tenant.ExpiresAt);
    }

    /// <summary>A shop with no end date can never be expired, no matter how far "now" is pushed.</summary>
    [Fact]
    public void An_Open_Ended_Shop_Is_Never_Expired()
    {
        Tenant tenant = ActiveTenant();

        Assert.Null(tenant.ExpiresAt);
        Assert.False(tenant.IsSubscriptionExpired(Now));
        Assert.False(tenant.IsSubscriptionExpired(Now.AddYears(50)));
    }

    /// <summary>The boundary: expiry is inclusive, so the exact instant is already "expired".</summary>
    [Fact]
    public void Expiry_Is_Inclusive_At_The_Exact_Instant()
    {
        Tenant tenant = ActiveTenant();
        tenant.Approve(Now, months: 1);
        DateTime expiry = tenant.ExpiresAt!.Value;

        Assert.False(tenant.IsSubscriptionExpired(expiry.AddTicks(-1)));
        Assert.True(tenant.IsSubscriptionExpired(expiry));
        Assert.True(tenant.IsSubscriptionExpired(expiry.AddTicks(1)));
    }

    /// <summary>Blocking is a support decision and leaves the paid period exactly as it was.</summary>
    [Fact]
    public void Blocking_And_Unblocking_Leave_The_Period_Untouched()
    {
        Tenant tenant = ActiveTenant();
        tenant.Approve(Now, months: 6);
        DateTime expiry = tenant.ExpiresAt!.Value;

        tenant.Block();
        Assert.Equal(expiry, tenant.ExpiresAt);
        Assert.Equal(TenantStatus.Blocked, tenant.Status);

        tenant.Activate();
        Assert.Equal(expiry, tenant.ExpiresAt);
        Assert.Equal(TenantStatus.Active, tenant.Status);
    }

    private static Tenant ActiveTenant() =>
        Tenant.Create("Test mağaza", "Sahibkar", "0700000000", TenantStatus.Active);
}
