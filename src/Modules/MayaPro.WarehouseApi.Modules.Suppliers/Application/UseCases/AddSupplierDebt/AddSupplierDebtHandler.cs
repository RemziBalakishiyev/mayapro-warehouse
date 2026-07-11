using FluentValidation;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.AddSupplierDebt;

/// <summary>
/// Records a purchase on credit: increases our debt to the supplier. In one transaction: increase the
/// debt, log the activity.
/// </summary>
public sealed class AddSupplierDebtHandler(
    ISuppliersDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<AddSupplierDebtCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<SupplierDto>> Handle(AddSupplierDebtCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<SupplierDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        Supplier? supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == command.SupplierId, ct);
        if (supplier is null)
            return Result.Failure<SupplierDto>(SupplierErrors.NotFound);

        supplier.IncreaseDebt(command.Amount);

        await tx.SaveChangesAsync(ct);

        await activityLogger.LogAsync(
            "Təchizatçı borcu artdı",
            $"{supplier.Name} — {command.Amount:0.00} AZN",
            currentUser.UserId,
            ct);

        await tx.CommitAsync(ct);

        return Result.Success(supplier.ToDto());
    }
}
