using FluentValidation;
using MayaPro.WarehouseApi.Modules.Sales.Domain;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;

/// <summary>
/// The rules a sale must satisfy however it is written — creating one and revising one apply exactly the same
/// checks (an update is a full reverse-and-reapply of the sale's values), so they are defined once here rather
/// than kept in sync by hand in two validators. Rule order is part of the contract: handlers surface only the
/// first error message, so the general shape checks run before the payment ones.
/// </summary>
public abstract class SaleWriteValidator<TCommand> : AbstractValidator<TCommand>
    where TCommand : ISaleWriteCommand
{
    protected SaleWriteValidator()
    {
        // Free-form (manual) sale: no product is chosen, so the name must be typed by hand.
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

        // BE#15 — qismən ödənişli satış: 0 ≤ paidAmount ≤ total. The total is the command's own
        // SalePrice × Quantity, which is exactly what the handler stores as TotalAmount, so the ceiling is
        // checked against the same figure the sale ends up with (never a client-supplied total).
        RuleFor(x => x.PaidAmount)
            .GreaterThanOrEqualTo(0m).When(x => x.PaidAmount is not null)
            .WithMessage("Ödənilən məbləğ mənfi ola bilməz");

        RuleFor(x => x.PaidAmount)
            .Must((command, paidAmount) => paidAmount is null || paidAmount <= Total(command))
            .WithMessage("Ödənilən məbləğ ümumi məbləğdən çox ola bilməz");

        RuleFor(x => x.PaidVia)
            .Must(code => code is null || code == PaymentTypeCode.Cash || code == PaymentTypeCode.Card)
            .WithMessage("Ödəniş üsulu Nağd və ya Kart olmalıdır");

        // A remaining balance (total − paid > 0) always needs a customer, whatever PaymentType was requested —
        // a partially (or un-)paid sale is a credit sale by definition (SalePaymentPlan).
        RuleFor(x => x)
            .Must(HaveCustomerWhenBalanceRemains)
            .WithMessage("Qalıq borc üçün müştəri seçilməlidir")
            .WithName(nameof(ISaleWriteCommand.CustomerId));
    }

    private static decimal Total(TCommand command) => command.SalePrice * command.Quantity;

    private static bool HaveCustomerWhenBalanceRemains(TCommand command)
    {
        if (!PaymentTypeCode.TryParse(command.PaymentType, out PaymentType requestedType))
            return true; // already flagged by the PaymentType rule above.

        SalePaymentPlan plan =
            SalePaymentPlan.Resolve(requestedType, Total(command), command.PaidAmount, command.PaidVia);
        return plan.Remaining <= 0 || command.CustomerId is not null;
    }
}
