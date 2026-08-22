using MayaPro.WarehouseApi.Modules.Settings.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Settings.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Settings.Application;

/// <summary>
/// The Settings module's implementation of <see cref="ISettingsModule"/>. Ensures the singleton row
/// exists (same first-read behaviour as the settings API) and returns the store display name.
/// <para>
/// BE#48 — the seeded default store name is the tenant's own registration name (via
/// <see cref="StoreNameSeed"/>), not a fixed brand placeholder.
/// </para>
/// </summary>
internal sealed class SettingsModuleContract(
    ISettingsDbContext db,
    ICurrentTenant currentTenant,
    ITenantDirectory tenantDirectory) : ISettingsModule
{
    public async Task<string> GetStoreNameAsync(CancellationToken cancellationToken = default) =>
        (await GetOrCreateAsync(cancellationToken)).StoreName;

    public async Task<StoreInfo> GetStoreInfoAsync(CancellationToken cancellationToken = default)
    {
        StoreSettings settings = await GetOrCreateAsync(cancellationToken);
        return new StoreInfo(settings.StoreName, settings.Address, settings.Phone, settings.Currency);
    }

    private async Task<StoreSettings> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        StoreSettings? settings = await db.StoreSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (settings is not null)
            return settings;

        string storeName = await StoreNameSeed.ResolveAsync(currentTenant, tenantDirectory, cancellationToken);
        settings = StoreSettings.CreateDefault(storeName);
        db.StoreSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }
}
