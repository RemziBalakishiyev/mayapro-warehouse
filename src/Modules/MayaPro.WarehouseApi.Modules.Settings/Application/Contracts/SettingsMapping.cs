using MayaPro.WarehouseApi.Modules.Settings.Domain;

namespace MayaPro.WarehouseApi.Modules.Settings.Application.Contracts;

/// <summary>Maps the <see cref="StoreSettings"/> entity to its wire DTO.</summary>
public static class SettingsMapping
{
    public static SettingsDto ToDto(this StoreSettings settings) =>
        new(
            settings.StoreName,
            settings.OwnerName,
            settings.WhatsappTemplate,
            settings.Currency,
            settings.DefaultMinStock,
            settings.Language);
}
