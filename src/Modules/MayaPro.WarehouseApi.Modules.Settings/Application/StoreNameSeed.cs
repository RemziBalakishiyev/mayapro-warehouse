using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Settings.Application;

/// <summary>
/// BE#48 — resolves the name a brand-new settings row should be seeded with: the current tenant's own
/// registration name, looked up through <see cref="ITenantDirectory"/> (Settings never reads the
/// <c>tenancy</c> schema directly — same cross-module rule as everywhere else). Falls back to
/// <see cref="BrandInfo.Name"/> only when no tenant could be resolved, which every Settings entry point
/// requires authentication and therefore should not normally reach — never a fixed store-name placeholder.
/// </summary>
internal static class StoreNameSeed
{
    public static async Task<string> ResolveAsync(
        ICurrentTenant currentTenant,
        ITenantDirectory tenantDirectory,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is { } tenantId)
        {
            TenantInfo? tenant = await tenantDirectory.FindAsync(tenantId, cancellationToken);
            if (tenant is not null)
                return tenant.Name;
        }

        return BrandInfo.Name;
    }
}
