using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.UpdateProduct;

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ad boş ola bilməz");

        RuleFor(x => x.PurchasePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Alış qiyməti mənfi ola bilməz");

        RuleFor(x => x.SalePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Satış qiyməti mənfi ola bilməz");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("Say mənfi ola bilməz");

        RuleFor(x => x.MinStock)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum stok mənfi ola bilməz");
    }
}
