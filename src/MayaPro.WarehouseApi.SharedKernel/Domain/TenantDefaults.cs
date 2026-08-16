namespace MayaPro.WarehouseApi.SharedKernel.Domain;

/// <summary>
/// The single tenant every pre-BE#35 row belongs to. The id is a fixed constant rather than a generated
/// value so that each module's data migration can back-fill its own tables independently (and idempotently)
/// without reading the <c>tenancy</c> schema — modules never touch another module's tables, not even in SQL.
/// </summary>
public static class TenantDefaults
{
    /// <summary>Deterministic id of the tenant that owns all data created before multi-tenancy.</summary>
    public static readonly Guid DefaultTenantId = new("00000000-0000-0000-0000-000000000001");

    /// <summary>Display name of that tenant, as written by the Tenancy module's seed migration.</summary>
    public const string DefaultTenantName = "İlk Mağaza";

    /// <summary>The same id as a SQL literal, for use inside migration statements.</summary>
    public const string DefaultTenantIdSql = "'00000000-0000-0000-0000-000000000001'";

    /// <summary>
    /// BE#36 — the reserved tenant id every <c>PlatformAdmin</c> user carries. It is deliberately a real,
    /// non-empty id that <b>no</b> <c>tenancy.Tenants</c> row ever uses:
    /// <list type="bullet">
    ///   <item>the column stays <c>NOT NULL</c> and the <c>(TenantId, Phone)</c> unique index keeps working,
    ///   so no shop-facing schema had to change for the platform operator;</item>
    ///   <item><c>TenantInterceptor</c> keeps its "empty means unset" contract — a reserved id is a set
    ///   value, so a platform admin's row is never mistaken for an unscoped one;</item>
    ///   <item>every tenant-scoped query an admin token could reach filters on this id and matches nothing,
    ///   because no business row is ever written under it. Fail-closed, not fail-open.</item>
    /// </list>
    /// The admin's own <c>identity.Users</c> row <i>is</i> under this id, which is what keeps
    /// <c>GET /api/auth/me</c> working for them without any filter bypass.
    /// </summary>
    public static readonly Guid PlatformTenantId = new("00000000-0000-0000-0000-0000000000ff");
}
