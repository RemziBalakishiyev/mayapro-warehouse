using FluentValidation;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.UseCases.RegisterTenant;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.CreateTenant;

public sealed class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.StoreName)
            .NotEmpty().WithMessage("Mağaza adı boş ola bilməz")
            .MaximumLength(200).WithMessage("Mağaza adı 200 simvoldan uzun ola bilməz");

        RuleFor(x => x.OwnerName)
            .NotEmpty().WithMessage("Sahibkar adı boş ola bilməz")
            .MaximumLength(200).WithMessage("Sahibkar adı 200 simvoldan uzun ola bilməz");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Telefon boş ola bilməz")
            .MaximumLength(30).WithMessage("Telefon 30 simvoldan uzun ola bilməz");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifrə boş ola bilməz")
            .MinimumLength(RegisterTenantValidator.MinPasswordLength)
            .WithMessage($"Şifrə ən azı {RegisterTenantValidator.MinPasswordLength} simvol olmalıdır");

        // Optional — but if a period is named it must be a real one. The upper bound stops a typo
        // ("12000" instead of "12") from handing out a century of free service.
        When(x => x.Months is not null, () => RuleFor(x => x.Months!.Value)
            .InclusiveBetween(1, Tenant.MaxPeriodMonths)
            .WithMessage($"Ay sayı 1 ilə {Tenant.MaxPeriodMonths} arasında olmalıdır"));

        When(x => x.MonthlyFee is not null, () => RuleFor(x => x.MonthlyFee!.Value)
            .GreaterThanOrEqualTo(0).WithMessage("Aylıq abunə haqqı mənfi ola bilməz"));
    }
}
