using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CreateCategory;

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Kateqoriya adı boş ola bilməz");
    }
}
