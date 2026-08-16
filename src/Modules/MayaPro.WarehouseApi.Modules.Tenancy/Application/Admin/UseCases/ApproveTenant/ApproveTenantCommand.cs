using FluentValidation;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.ApproveTenant;

/// <summary>
/// Body of <c>POST /api/admin/tenants/{id}/approve</c>: how many months the shop is approved for.
/// <para>
/// BE#42 — the field is <c>periodMonths</c>, the same name the payment history already answers with
/// (<c>SubscriptionPaymentDto.PeriodMonths</c>) and the name the specification uses. No <c>months</c> alias
/// exists: the endpoint had no consumer when the name was corrected.
/// </para>
/// </summary>
public sealed record ApproveTenantCommand(int PeriodMonths);

public sealed class ApproveTenantValidator : AbstractValidator<ApproveTenantCommand>
{
    public ApproveTenantValidator()
    {
        RuleFor(x => x.PeriodMonths)
            .InclusiveBetween(1, Tenant.MaxPeriodMonths)
            .WithMessage($"Ay sayı 1 ilə {Tenant.MaxPeriodMonths} arasında olmalıdır");
    }
}
