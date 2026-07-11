using FluentValidation;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.CreateSupplier;

/// <summary>Creates a supplier with an optional opening debt, and records an activity entry.</summary>
public sealed class CreateSupplierHandler(
    ISuppliersDbContext db,
    IValidator<CreateSupplierCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<SupplierDto>> Handle(CreateSupplierCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<SupplierDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        var supplier = Supplier.Create(command.Name, command.ContactName, command.Phone, command.Note, command.Debt);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);

        await activityLogger.LogAsync("Təchizatçı əlavə etdi", supplier.Name, currentUser.UserId, ct);

        return Result.Success(supplier.ToDto());
    }
}
