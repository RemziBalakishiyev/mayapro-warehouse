using MayaPro.WarehouseApi.Modules.Settings.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Settings.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Settings.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Settings.Application.UseCases.GetSettings;

/// <summary>
/// Returns the store settings. The settings are a singleton: on first ever read the row does not exist
/// yet, so it is created with defaults and persisted before being returned.
/// <para>
/// BE#48 — the seeded default store name is the tenant's own registration name (via
/// <see cref="StoreNameSeed"/>), not a fixed brand placeholder: a brand-new shop sees its own name on first
/// read.
/// </para>
/// </summary>
public sealed class GetSettingsHandler(
    ISettingsDbContext db,
    ICurrentTenant currentTenant,
    ITenantDirectory tenantDirectory)
{
    public async Task<SettingsDto> Handle(CancellationToken ct)
    {
        StoreSettings? settings = await db.StoreSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is not null)
            return settings.ToDto();

        string storeName = await StoreNameSeed.ResolveAsync(currentTenant, tenantDirectory, ct);
        settings = StoreSettings.CreateDefault(storeName);
        db.StoreSettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings.ToDto();
    }
}
