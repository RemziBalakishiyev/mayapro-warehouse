namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>
/// The Tenancy module's public surface: look up a tenant's registration status. Used by the Auth module at
/// login time and by the host's per-request tenant gate — neither of them may read the <c>tenancy</c> schema
/// directly (modules never touch another module's tables).
/// </summary>
public interface ITenantDirectory
{
    /// <summary>Returns the tenant, or <c>null</c> when no tenant carries that id.</summary>
    Task<TenantInfo?> FindAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A tenant as other modules see it. <see cref="Status"/> is the enum name
/// (<c>PendingApproval</c> / <c>Active</c> / <c>Blocked</c>); <see cref="IsActive"/> and
/// <see cref="ExpiresAt"/> together are what the access decision is made of.
/// </summary>
/// <param name="ExpiresAt">
/// BE#36 — when the paid subscription runs out, in UTC. <c>null</c> means <b>open-ended</b>: such a shop is
/// never treated as expired (the pre-existing installation and any shop the admin activates without naming a
/// period land here). Carried on this record on purpose, so the per-request gate answers "is this shop still
/// allowed in?" with the same single lookup it already did — no second query.
/// </param>
public sealed record TenantInfo(Guid Id, string Name, string Status, bool IsActive, DateTime? ExpiresAt)
{
    /// <summary>
    /// The subscription rule in one place: a shop is expired only when it named an end date and that date
    /// has passed. <c>null</c> (open-ended) is never expired.
    /// </summary>
    public bool IsSubscriptionExpired(DateTime utcNow) => ExpiresAt is { } expiry && expiry <= utcNow;

    /// <summary>The full access decision: approved <b>and</b> still paid up.</summary>
    public bool MayAccess(DateTime utcNow) => IsActive && !IsSubscriptionExpired(utcNow);
}
