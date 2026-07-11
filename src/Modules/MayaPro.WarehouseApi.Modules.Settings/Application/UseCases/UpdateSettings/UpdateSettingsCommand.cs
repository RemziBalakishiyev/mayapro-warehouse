namespace MayaPro.WarehouseApi.Modules.Settings.Application.UseCases.UpdateSettings;

/// <summary>The editable store settings. Sent as the body of <c>PUT /api/settings</c>.</summary>
public sealed record UpdateSettingsCommand(
    string StoreName,
    string? OwnerName,
    string WhatsappTemplate,
    string Currency,
    int DefaultMinStock,
    string Language);
