using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.UpdateCustomer;

public sealed class UpdateCustomerValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ad boş ola bilməz");
    }
}
