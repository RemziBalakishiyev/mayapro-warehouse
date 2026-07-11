using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Application.UseCases.CloseDay;

public sealed class CloseDayValidator : AbstractValidator<CloseDayCommand>
{
    public CloseDayValidator()
    {
        RuleFor(x => x.OpeningCash)
            .GreaterThanOrEqualTo(0).WithMessage("Açılış kassası mənfi ola bilməz");

        RuleFor(x => x.ActualCash)
            .GreaterThanOrEqualTo(0).WithMessage("Faktiki kassa mənfi ola bilməz");
    }
}
