using FluentValidation;
using MayaPro.WarehouseApi.Modules.Sales.Domain;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.UpdateSale;

/// <summary>Same rules as creating a sale — an update is a full reverse-and-reapply of the sale's values.</summary>
public sealed class UpdateSaleValidator : AbstractValidator<UpdateSaleCommand>
{
    public UpdateSaleValidator()
    {
        RuleFor(x => x.ProductName)
            .NotEmpty().When(x => x.ProductId is null)
            .WithMessage("Sərbəst satışda mal adı məcburidir");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(1).WithMessage("Say ən azı 1 olmalıdır");

        RuleFor(x => x.SalePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Qiymət mənfi ola bilməz");

        RuleFor(x => x.Discount)
            .GreaterThanOrEqualTo(0).WithMessage("Endirim mənfi ola bilməz");

        RuleFor(x => x)
            .Must(x => x.Discount <= x.SalePrice * x.Quantity)
            .WithMessage("Endirim satış məbləğindən çox ola bilməz");

        RuleFor(x => x.PaymentType)
            .Must(code => PaymentTypeCode.TryParse(code, out _))
            .WithMessage("Ödəniş növü yanlışdır");

        RuleFor(x => x.CustomerId)
            .NotNull().When(x => x.PaymentType == PaymentTypeCode.Credit)
            .WithMessage("Nisyə satış üçün müştəri seçilməlidir");
    }
}
