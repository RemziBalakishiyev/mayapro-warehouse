using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.CreateCustomer;

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ad boş ola bilməz");

        RuleFor(x => x.Debt)
            .GreaterThanOrEqualTo(0).WithMessage("Borc mənfi ola bilməz");
    }
}
