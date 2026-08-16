using FluentValidation;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.ApproveTenant;

/// <summary>Body of <c>POST /api/admin/tenants/{id}/approve</c>: how many months the shop is approved for.</summary>
public sealed record ApproveTenantCommand(int Months);

public sealed class ApproveTenantValidator : AbstractValidator<ApproveTenantCommand>
{
    public ApproveTenantValidator()
    {
        RuleFor(x => x.Months)
            .InclusiveBetween(1, Tenant.MaxPeriodMonths)
            .WithMessage($"Ay sayı 1 ilə {Tenant.MaxPeriodMonths} arasında olmalıdır");
    }
}
