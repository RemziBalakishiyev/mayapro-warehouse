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
/// </summary>
public sealed class Tenant : Entity
{
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

    /// <summary>Who signed the shop up. Optional in Mərhələ 1 — no registration flow exists yet.</summary>
    public string? OwnerName { get; private set; }

    /// <summary>Contact phone for the shop's owner. Not a login identifier — see <c>identity.Users</c>.</summary>
    public string? Phone { get; private set; }

    public TenantStatus Status { get; private set; }

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
}
