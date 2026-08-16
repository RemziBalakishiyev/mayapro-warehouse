using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Domain;

/// <summary>
/// One customer of the SaaS — a shop (mağaza) whose data every other module isolates by
/// <see cref="Entity.Id"/>. Deliberately <b>not</b> tenant-scoped itself: this is the registry the scoping
/// is defined by, so it derives from <see cref="Entity"/>, not <c>TenantEntity</c>.
/// <para>
/// The type carries no navigation to any other module's entity — the link is always a bare
/// <c>TenantId</c> Guid on the other side, exactly like <c>Sale.CustomerId</c>.
/// </para>
/// <para>
/// BE#36 added the subscription: <see cref="ExpiresAt"/> and <see cref="MonthlyFee"/>. All the date
/// arithmetic lives here as pure methods that take "now" as an argument — no <c>DateTime.UtcNow</c> in the
/// domain, so every rule below is unit-testable without a clock.
/// </para>
/// </summary>
public sealed class Tenant : Entity
{
    /// <summary>Upper bound for any single subscription period, shared by the admin validators.</summary>
    public const int MaxPeriodMonths = 120;

    // EF Core constructor.
    private Tenant() { }

    private Tenant(Guid id, string name, string? ownerName, string? phone, TenantStatus status)
    {
        Id = id;
        Name = name;
        OwnerName = ownerName;
        Phone = phone;
        Status = status;
    }

    /// <summary>The shop's display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Who signed the shop up.</summary>
    public string? OwnerName { get; private set; }

    /// <summary>Contact phone for the shop's owner. Not a login identifier — see <c>identity.Users</c>.</summary>
    public string? Phone { get; private set; }

    public TenantStatus Status { get; private set; }

    /// <summary>
    /// End of the paid period, in UTC. <c>null</c> means <b>open-ended</b> — the shop is never treated as
    /// expired. Every shop that existed before BE#36 (and the default "İlk Mağaza") keeps <c>null</c>, so an
    /// installation that upgrades does not lock itself out overnight.
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>The agreed monthly fee. Zero means "not agreed yet"; never null, so sums stay simple.</summary>
    public decimal MonthlyFee { get; private set; }

    /// <summary>True only for <see cref="TenantStatus.Active"/> — the single gate other modules ask about.</summary>
    public bool IsActive => Status == TenantStatus.Active;

    public static Tenant Create(
        string name,
        string? ownerName = null,
        string? phone = null,
        TenantStatus status = TenantStatus.PendingApproval) =>
        new(Guid.NewGuid(), name, ownerName, phone, status);

    /// <summary>Creates a tenant with a caller-chosen id — used by seeds/tests that need a fixed id.</summary>
    public static Tenant CreateWithId(
        Guid id,
        string name,
        string? ownerName = null,
        string? phone = null,
        TenantStatus status = TenantStatus.PendingApproval) =>
        new(id, name, ownerName, phone, status);

    public void Activate() => Status = TenantStatus.Active;

    public void Block() => Status = TenantStatus.Blocked;

    public void MarkPendingApproval() => Status = TenantStatus.PendingApproval;

    public void SetMonthlyFee(decimal monthlyFee) => MonthlyFee = monthlyFee;

    /// <summary>
    /// Approval: the shop becomes <see cref="TenantStatus.Active"/> and its period starts <b>now</b>, not
    /// from whatever stale date it may carry. Approving is a fresh start, which is why it assigns rather than
    /// extends (<see cref="Extend"/> is the one that adds to an existing period).
    /// </summary>
    public void Approve(DateTime utcNow, int months)
    {
        Status = TenantStatus.Active;
        ExpiresAt = utcNow.AddMonths(months);
    }

    /// <summary>
    /// Extends the paid period by <paramref name="months"/>:
    /// <c>ExpiresAt = max(now, current ExpiresAt ?? now) + months</c>.
    /// <list type="bullet">
    ///   <item>period still running → the new months are added <b>on top of</b> the remaining time, so paying
    ///   early never costs the customer the days they already own;</item>
    ///   <item>period already lapsed → the clock restarts from now, so a shop that paid late does not get a
    ///   period that is partly in the past;</item>
    ///   <item>open-ended (<c>null</c>) → treated as "starts now", which is the only sane reading: the shop
    ///   had no end date, and from this payment on it does.</item>
    /// </list>
    /// </summary>
    public void Extend(DateTime utcNow, int months)
    {
        DateTime from = ExpiresAt is { } expiry && expiry > utcNow ? expiry : utcNow;
        ExpiresAt = from.AddMonths(months);
    }

    /// <summary>Drops the end date — the shop becomes open-ended and can never expire.</summary>
    public void MakeOpenEnded() => ExpiresAt = null;

    /// <summary>
    /// True when the shop named an end date and it has passed. Open-ended shops are never expired. The
    /// status is deliberately <b>not</b> changed by expiry: <see cref="ExpiresAt"/> is the verdict, so
    /// recording a payment re-opens the shop with no status bookkeeping.
    /// </summary>
    public bool IsSubscriptionExpired(DateTime utcNow) => ExpiresAt is { } expiry && expiry <= utcNow;
}
