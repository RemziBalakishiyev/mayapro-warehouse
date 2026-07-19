using FluentValidation;
using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.CreateCustomer;

/// <summary>
/// Creates a customer with an optional opening debt. Everything commits in one transaction: the customer,
/// and — when an opening debt is supplied — the auditable opening-balance adjustment and an activity entry
/// naming the amount. Without an opening debt only the customer and a plain "created" activity are written.
/// </summary>
public sealed class CreateCustomerHandler(
    ICustomersDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<CreateCustomerCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<CustomerDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        var customer = Customer.Create(command.Name, command.Phone, command.Note, command.InitialDebt);

        // One transaction so the customer, its opening-balance adjustment and the activity log commit together.
        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        db.Customers.Add(customer);

        if (command.InitialDebt > 0)
        {
            var adjustment = CustomerDebtAdjustment.Create(
                customer.Id, command.InitialDebt, CustomerDebtAdjustment.InitialDebtNote, currentUser.UserId);
            db.CustomerDebtAdjustments.Add(adjustment);

            await activityLogger.LogAsync(
                "Müştəri əlavə etdi",
                $"{customer.Name} — ilkin borc {command.InitialDebt:0.00} AZN",
                currentUser.UserId,
                ct);
        }
        else
        {
            await activityLogger.LogAsync("Müştəri əlavə etdi", customer.Name, currentUser.UserId, ct);
        }

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(customer.ToDto(initialDebt: command.InitialDebt));
    }
}
