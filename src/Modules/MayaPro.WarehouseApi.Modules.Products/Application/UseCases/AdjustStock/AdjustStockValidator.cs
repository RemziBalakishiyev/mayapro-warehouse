using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.AdjustStock;

public sealed class AdjustStockValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockValidator()
    {
        RuleFor(x => x.Delta)
            .NotEqual(0).WithMessage("Dəyişiklik miqdarı sıfır ola bilməz");
    }
}
