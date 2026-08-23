using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.CreateEmployee;

/// <summary>
/// BE#57 — the maximum lengths mirror <c>EmployeeConfiguration</c> exactly, so an over-long value fails here
/// (400, in Azerbaijani) instead of in the database (500). <c>Phone</c> has no rule at all: it is optional,
/// not unique, and its format is checked by <c>PhoneNormalizer</c> in the handler.
/// </summary>
public sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Ad boş ola bilməz")
            .MaximumLength(200).WithMessage("Ad 200 simvoldan uzun ola bilməz");

        RuleFor(x => x.Position)
            .NotEmpty().WithMessage("Vəzifə boş ola bilməz")
            .MaximumLength(100).WithMessage("Vəzifə 100 simvoldan uzun ola bilməz");

        // Zero is legitimate ("no salary agreed yet"); negative never is.
        RuleFor(x => x.MonthlySalary)
            .GreaterThanOrEqualTo(0).WithMessage("Maaş mənfi ola bilməz");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Qeyd 500 simvoldan uzun ola bilməz");
    }
}
