using FluentValidation;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.RecordPayment;

/// <summary>
/// Body of <c>POST /api/admin/tenants/{id}/payments</c>: the money received and what it buys.
/// <para>
/// BE#42 — <c>periodMonths</c> matches the field the history endpoint returns for the very row this
/// creates; the request and the response now name the same concept the same way.
/// </para>
/// </summary>
public sealed record RecordPaymentCommand(decimal Amount, int PeriodMonths, string? Note);

public sealed class RecordPaymentValidator : AbstractValidator<RecordPaymentCommand>
{
    /// <summary>A sanity ceiling, not a business limit — it only exists to catch a slipped decimal point.</summary>
    public const decimal MaxAmount = 1_000_000m;

    public RecordPaymentValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Məbləğ sıfırdan böyük olmalıdır")
            .LessThanOrEqualTo(MaxAmount).WithMessage($"Məbləğ {MaxAmount:0} -dan böyük ola bilməz");

        RuleFor(x => x.PeriodMonths)
            .InclusiveBetween(1, Tenant.MaxPeriodMonths)
            .WithMessage($"Ay sayı 1 ilə {Tenant.MaxPeriodMonths} arasında olmalıdır");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Qeyd 500 simvoldan uzun ola bilməz");
    }
}
