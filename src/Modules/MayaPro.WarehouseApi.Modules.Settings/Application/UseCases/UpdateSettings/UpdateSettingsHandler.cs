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

        StoreSettings? settings = await db.StoreSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = StoreSettings.CreateDefault();
            db.StoreSettings.Add(settings);
        }

        settings.Update(
            command.StoreName,
            command.OwnerName,
            command.WhatsappTemplate,
            command.Currency,
            command.DefaultMinStock,
            command.Language);

        await db.SaveChangesAsync(ct);
        return Result.Success(settings.ToDto());
    }
}
