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
/// (<c>PendingApproval</c> / <c>Active</c> / <c>Blocked</c>); <see cref="IsActive"/> is the only thing
/// callers need for the access decision.
/// </summary>
public sealed record TenantInfo(Guid Id, string Name, string Status, bool IsActive);
