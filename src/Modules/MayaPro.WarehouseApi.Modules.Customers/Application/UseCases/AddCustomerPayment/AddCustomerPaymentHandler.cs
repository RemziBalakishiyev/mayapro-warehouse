using FluentValidation;
using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.AddCustomerPayment;

/// <summary>
/// Records a payment against a customer's debt — the second half of the debt chain. In one transaction:
/// reduce the debt (fails if the payment exceeds it), write the payment, log the activity.
/// </summary>
public sealed class AddCustomerPaymentHandler(
    ICustomersDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<AddCustomerPaymentCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<CustomerPaymentDto>> Handle(AddCustomerPaymentCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<CustomerPaymentDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        Customer? customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == command.CustomerId, ct);
        if (customer is null)
            return Result.Failure<CustomerPaymentDto>(CustomerErrors.NotFound);

        Result decrease = customer.DecreaseDebt(command.Amount);
        if (decrease.IsFailure)
            return Result.Failure<CustomerPaymentDto>(decrease.Error);

        var payment = CustomerPayment.Create(customer.Id, command.Amount, command.Note, currentUser.UserId);
        db.CustomerPayments.Add(payment);

        // Log before the save so the activity is flushed in the same transaction.
        await activityLogger.LogAsync(
            "Ödəniş qəbul etdi",
            $"{customer.Name} — {command.Amount:0.00} AZN",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(payment.ToDto());
    }
}
