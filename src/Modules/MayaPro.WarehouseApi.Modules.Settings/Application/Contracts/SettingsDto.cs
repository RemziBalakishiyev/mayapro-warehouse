namespace MayaPro.WarehouseApi.Modules.Settings.Application.Contracts;

/// <summary>
/// The store settings as returned by the API. Field names mirror the frontend settings store
/// (camelCase on the wire).
/// </summary>
public sealed record SettingsDto(
    string StoreName,
    string? OwnerName,
    string WhatsappTemplate,
    string Currency,
    int DefaultMinStock,
    string Language);
