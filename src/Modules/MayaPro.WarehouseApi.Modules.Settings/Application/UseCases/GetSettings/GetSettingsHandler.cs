using MayaPro.WarehouseApi.Modules.Settings.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Settings.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Settings.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Settings.Application.UseCases.GetSettings;

/// <summary>
/// Returns the store settings. The settings are a singleton: on first ever read the row does not exist
/// yet, so it is created with defaults and persisted before being returned.
/// </summary>
public sealed class GetSettingsHandler(ISettingsDbContext db)
{
    public async Task<SettingsDto> Handle(CancellationToken ct)
    {
        StoreSettings? settings = await db.StoreSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is not null)
            return settings.ToDto();

        settings = StoreSettings.CreateDefault();
        db.StoreSettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings.ToDto();
    }
}
