using FluentValidation;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.AddSupplierPayment;

/// <summary>
/// Records a payment against a supplier's debt. In one transaction: reduce the debt (fails if the payment
/// exceeds it), write the payment, log the activity.
/// </summary>
public sealed class AddSupplierPaymentHandler(
    ISuppliersDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<AddSupplierPaymentCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<SupplierPaymentDto>> Handle(AddSupplierPaymentCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<SupplierPaymentDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        Supplier? supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == command.SupplierId, ct);
        if (supplier is null)
            return Result.Failure<SupplierPaymentDto>(SupplierErrors.NotFound);

        Result decrease = supplier.DecreaseDebt(command.Amount);
        if (decrease.IsFailure)
            return Result.Failure<SupplierPaymentDto>(decrease.Error);

        var payment = SupplierPayment.Create(supplier.Id, command.Amount, command.Note, currentUser.UserId);
        db.SupplierPayments.Add(payment);

        // Log before the save so the activity is flushed in the same transaction.
        await activityLogger.LogAsync(
            "Təchizatçıya ödəniş etdi",
            $"{supplier.Name} — {command.Amount:0.00} AZN",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(payment.ToDto());
    }
}
