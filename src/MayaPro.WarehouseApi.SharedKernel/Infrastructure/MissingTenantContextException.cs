namespace MayaPro.WarehouseApi.SharedKernel.Infrastructure;

/// <summary>
/// Thrown by <see cref="TenantInterceptor"/> when tenant-scoped rows are about to be inserted without a
/// tenant context. Failing loudly is the point: silently writing <c>Guid.Empty</c> would produce rows that
/// belong to nobody, are invisible to every tenant, and quietly corrupt the isolation guarantee.
/// </summary>
public sealed class MissingTenantContextException(string entityName)
    : InvalidOperationException(
        $"'{entityName}' tenant konteksti olmadan yazıla bilməz — cari sorğuda mağaza (tenant) təyin edilməyib.")
{
    /// <summary>The CLR name of the entity type that could not be stamped.</summary>
    public string EntityName { get; } = entityName;
}
