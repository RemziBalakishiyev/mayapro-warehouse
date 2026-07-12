using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CreateProduct;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
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

        RuleFor(x => x.Attributes)
            .Must(a => a is null || a.Count <= 15).WithMessage("Ən çoxu 15 xüsusiyyət əlavə etmək olar");

        RuleForEach(x => x.Attributes)
            .Must(a => !string.IsNullOrWhiteSpace(a.Name))
            .WithMessage("Xüsusiyyət adı boş ola bilməz");
    }
}
