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
}
