using FluentValidation;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.UpdateSupplier;

/// <summary>
/// Edits a supplier's details and logs the change, in one transaction so the edit and its activity entry
/// commit together. Debt is untouched.
/// </summary>
public sealed class UpdateSupplierHandler(
    ISuppliersDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<UpdateSupplierCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<SupplierDto>> Handle(UpdateSupplierCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<SupplierDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        Supplier? supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (supplier is null)
            return Result.Failure<SupplierDto>(SupplierErrors.NotFound);

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        supplier.Update(command.Name, command.ContactName, command.Phone, command.Note);

        await activityLogger.LogAsync("Təchizatçını düzəltdi", supplier.Name, currentUser.UserId, ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(supplier.ToDto());
    }
}
