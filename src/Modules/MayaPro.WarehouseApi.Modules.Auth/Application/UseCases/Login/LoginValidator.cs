using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Telefon boş ola bilməz");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifrə boş ola bilməz");
    }
}
