using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.CreateSupplier;

public sealed class CreateSupplierValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ad boş ola bilməz");

        RuleFor(x => x.Debt)
            .GreaterThanOrEqualTo(0).WithMessage("Borc mənfi ola bilməz");
    }
}
