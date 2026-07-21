using FluentValidation;
using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.UpdateCustomer;

/// <summary>
/// Edits a customer's details and logs the change, in one transaction so the edit and its activity entry
/// commit together. Debt is untouched — only name/phone/note change.
/// </summary>
public sealed class UpdateCustomerHandler(
    ICustomersDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<UpdateCustomerCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<CustomerDto>> Handle(UpdateCustomerCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<CustomerDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        Customer? customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == command.Id, ct);
        if (customer is null)
            return Result.Failure<CustomerDto>(CustomerErrors.NotFound);

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        customer.Update(command.Name, command.Phone, command.Note);

        await activityLogger.LogAsync("Müştərini düzəltdi", customer.Name, currentUser.UserId, ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(customer.ToDto());
    }
}
