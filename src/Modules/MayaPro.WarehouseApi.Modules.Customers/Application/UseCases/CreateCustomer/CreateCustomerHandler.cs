using FluentValidation;
using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.CreateCustomer;

/// <summary>Creates a customer with an optional opening debt.</summary>
public sealed class CreateCustomerHandler(
    ICustomersDbContext db,
    IValidator<CreateCustomerCommand> validator)
{
    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<CustomerDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        var customer = Customer.Create(command.Name, command.Phone, command.Note, command.Debt);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return Result.Success(customer.ToDto());
    }
}
