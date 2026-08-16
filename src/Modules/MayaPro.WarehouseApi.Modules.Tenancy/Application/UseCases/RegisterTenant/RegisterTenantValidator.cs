using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.UseCases.RegisterTenant;

/// <summary>
/// Every length here mirrors a column, so an over-long value is a 400 with an Azerbaijani message rather
/// than a database error surfacing as a 500.
/// </summary>
public sealed class RegisterTenantValidator : AbstractValidator<RegisterTenantCommand>
{
    /// <summary>Shared with the admin's own "create shop" form — one rule, two entry points.</summary>
    public const int MinPasswordLength = 6;

    public RegisterTenantValidator()
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
            .MinimumLength(MinPasswordLength)
            .WithMessage($"Şifrə ən azı {MinPasswordLength} simvol olmalıdır");
    }
}
