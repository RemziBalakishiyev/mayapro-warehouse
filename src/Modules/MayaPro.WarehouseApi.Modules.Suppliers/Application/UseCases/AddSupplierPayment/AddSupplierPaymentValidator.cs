using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.AddSupplierPayment;

public sealed class AddSupplierPaymentValidator : AbstractValidator<AddSupplierPaymentCommand>
{
    public AddSupplierPaymentValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Ödəniş məbləği sıfırdan böyük olmalıdır");
    }
}
