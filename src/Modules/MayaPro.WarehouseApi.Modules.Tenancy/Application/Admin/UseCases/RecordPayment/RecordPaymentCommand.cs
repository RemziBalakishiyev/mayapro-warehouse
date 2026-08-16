using FluentValidation;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.RecordPayment;

/// <summary>Body of <c>POST /api/admin/tenants/{id}/payments</c>: the money received and what it buys.</summary>
public sealed record RecordPaymentCommand(decimal Amount, int Months, string? Note);

public sealed class RecordPaymentValidator : AbstractValidator<RecordPaymentCommand>
{
    /// <summary>A sanity ceiling, not a business limit — it only exists to catch a slipped decimal point.</summary>
    public const decimal MaxAmount = 1_000_000m;

    public RecordPaymentValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Məbləğ sıfırdan böyük olmalıdır")
            .LessThanOrEqualTo(MaxAmount).WithMessage($"Məbləğ {MaxAmount:0} -dan böyük ola bilməz");

        RuleFor(x => x.Months)
            .InclusiveBetween(1, Tenant.MaxPeriodMonths)
            .WithMessage($"Ay sayı 1 ilə {Tenant.MaxPeriodMonths} arasında olmalıdır");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Qeyd 500 simvoldan uzun ola bilməz");
    }
}
