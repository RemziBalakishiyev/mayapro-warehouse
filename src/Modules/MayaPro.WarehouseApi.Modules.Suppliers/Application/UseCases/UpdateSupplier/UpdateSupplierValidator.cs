using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.UpdateSupplier;

public sealed class UpdateSupplierValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ad boş ola bilməz");
    }
}
