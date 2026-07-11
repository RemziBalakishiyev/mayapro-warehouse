using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.AddCustomerPayment;

public sealed class AddCustomerPaymentValidator : AbstractValidator<AddCustomerPaymentCommand>
{
    public AddCustomerPaymentValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Ödəniş məbləği sıfırdan böyük olmalıdır");
    }
}
