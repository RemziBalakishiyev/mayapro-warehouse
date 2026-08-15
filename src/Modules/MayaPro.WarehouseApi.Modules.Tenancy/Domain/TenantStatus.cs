namespace MayaPro.WarehouseApi.Modules.Tenancy.Domain;

/// <summary>
/// A tenant's registration state. Only <see cref="Active"/> may sign in; the other two exist so that
/// Mərhələ 2 (self-service registration) and support-side suspension have somewhere to land.
/// </summary>
public enum TenantStatus
{
    /// <summary>Registered but not yet approved — cannot sign in.</summary>
    PendingApproval = 0,

    /// <summary>Approved and paying/trialling — normal access.</summary>
    Active = 1,

    /// <summary>Suspended by support (non-payment, abuse) — cannot sign in.</summary>
    Blocked = 2
}
