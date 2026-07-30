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

        RuleFor(x => x.PaymentType)
            .Must(code => PaymentTypeCode.TryParse(code, out _))
            .WithMessage("Ödəniş növü yanlışdır");

        RuleFor(x => x.PurchasePricePerUnit)
            .GreaterThanOrEqualTo(0m).When(x => x.PurchasePricePerUnit is not null)
            .WithMessage("Alış qiyməti mənfi ola bilməz");

        // BE#15 — qismən ödənişli satış: 0 ≤ paidAmount ≤ total.
        RuleFor(x => x.PaidAmount)
            .GreaterThanOrEqualTo(0m).When(x => x.PaidAmount is not null)
            .WithMessage("Ödənilən məbləğ mənfi ola bilməz");

        RuleFor(x => x.PaidAmount)
            .Must((command, paidAmount) => paidAmount is null || paidAmount <= command.SalePrice * command.Quantity)
            .WithMessage("Ödənilən məbləğ ümumi məbləğdən çox ola bilməz");

        RuleFor(x => x.PaidVia)
            .Must(code => code is null || code == PaymentTypeCode.Cash || code == PaymentTypeCode.Card)
            .WithMessage("Ödəniş üsulu Nağd və ya Kart olmalıdır");

        // A remaining balance (total − paid > 0) always needs a customer, whatever PaymentType was requested —
        // a partially (or un-)paid sale is a credit sale by definition (SalePaymentPlan).
        RuleFor(x => x)
            .Must(HaveCustomerWhenBalanceRemains)
            .WithMessage("Qalıq borc üçün müştəri seçilməlidir")
            .WithName(nameof(UpdateSaleCommand.CustomerId));
    }

    private static bool HaveCustomerWhenBalanceRemains(UpdateSaleCommand command)
    {
        if (!PaymentTypeCode.TryParse(command.PaymentType, out PaymentType requestedType))
            return true; // already flagged by the PaymentType rule above.

        decimal total = command.SalePrice * command.Quantity;
        SalePaymentPlan plan = SalePaymentPlan.Resolve(requestedType, total, command.PaidAmount, command.PaidVia);
        return plan.Remaining <= 0 || command.CustomerId is not null;
    }
}
