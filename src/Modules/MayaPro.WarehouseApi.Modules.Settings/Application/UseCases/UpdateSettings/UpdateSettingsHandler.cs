using FluentValidation;
using MayaPro.WarehouseApi.Modules.Settings.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Settings.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Settings.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Settings.Application.UseCases.UpdateSettings;

/// <summary>
/// Updates the store settings (owner only). Works on the singleton: if it does not exist yet it is
/// created first, then the new values are applied and saved.
/// </summary>
public sealed class UpdateSettingsHandler(
    ISettingsDbContext db,
    IValidator<UpdateSettingsCommand> validator)
{
    public async Task<Result<SettingsDto>> Handle(UpdateSettingsCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<SettingsDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        // BE#46 — the store phone printed on invoice headers is stored canonically like every other phone.
        // Checked before the settings row is created, so a bad phone never leaves a half-written default row
        // behind on the very first save.
        Result<string?> phone = PhoneNormalizer.NormalizeOptional(command.Phone);
        if (phone.IsFailure)
            return Result.Failure<SettingsDto>(phone.Error);

        StoreSettings? settings = await db.StoreSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = StoreSettings.CreateDefault();
            db.StoreSettings.Add(settings);
        }

        settings.Update(
            command.StoreName,
            command.OwnerName,
            command.Address,
            phone.Value,
            command.WhatsappTemplate,
            command.Currency,
            command.DefaultMinStock,
            command.Language);

        await db.SaveChangesAsync(ct);
        return Result.Success(settings.ToDto());
    }
}
