using FluentValidation;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.CreateSupplier;

/// <summary>
/// Creates a supplier with an optional opening debt. Everything commits in one transaction: the supplier,
/// and — when an opening debt is supplied — the auditable opening-balance adjustment and an activity entry
/// naming the amount. Without an opening debt only the supplier and a plain "created" activity are written.
/// </summary>
public sealed class CreateSupplierHandler(
    ISuppliersDbContext db,
    IUnitOfWork unitOfWork,
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

        // One transaction so the supplier, its opening-balance adjustment and the activity log commit together.
        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        db.Suppliers.Add(supplier);

        if (command.Debt > 0)
        {
            var adjustment = SupplierDebtAdjustment.Create(
                supplier.Id, command.Debt, SupplierDebtAdjustment.InitialDebtNote, currentUser.UserId);
            db.SupplierDebtAdjustments.Add(adjustment);

            await activityLogger.LogAsync(
                "Təchizatçı əlavə etdi",
                $"{supplier.Name} — ilkin borc {command.Debt:0.00} AZN",
                currentUser.UserId,
                ct);
        }
        else
        {
            await activityLogger.LogAsync("Təchizatçı əlavə etdi", supplier.Name, currentUser.UserId, ct);
        }

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(supplier.ToDto());
    }
}
