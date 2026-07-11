using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.AddSupplierDebt;

public sealed class AddSupplierDebtValidator : AbstractValidator<AddSupplierDebtCommand>
{
    public AddSupplierDebtValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Məbləğ sıfırdan böyük olmalıdır");
    }
}
