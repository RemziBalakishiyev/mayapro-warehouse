using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Settings.Application.UseCases.UpdateSettings;

public sealed class UpdateSettingsValidator : AbstractValidator<UpdateSettingsCommand>
{
    public UpdateSettingsValidator()
    {
        RuleFor(x => x.StoreName)
            .NotEmpty().WithMessage("Mağaza adı boş ola bilməz")
            .MaximumLength(200).WithMessage("Mağaza adı 200 simvoldan çox ola bilməz");

        RuleFor(x => x.OwnerName)
            .MaximumLength(200).WithMessage("Sahib adı 200 simvoldan çox ola bilməz");

        RuleFor(x => x.WhatsappTemplate)
            .NotEmpty().WithMessage("WhatsApp şablonu boş ola bilməz")
            .MaximumLength(1000).WithMessage("WhatsApp şablonu 1000 simvoldan çox ola bilməz");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Valyuta boş ola bilməz")
            .MaximumLength(10).WithMessage("Valyuta 10 simvoldan çox ola bilməz");

        RuleFor(x => x.DefaultMinStock)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum stok mənfi ola bilməz");

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage("Dil boş ola bilməz")
            .MaximumLength(10).WithMessage("Dil kodu 10 simvoldan çox ola bilməz");
    }
}
