using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.UpdateEmployee;

/// <summary>Same rules as <c>CreateEmployeeValidator</c> — the edit form is the create form (BE#57).</summary>
public sealed class UpdateEmployeeValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Ad boş ola bilməz")
            .MaximumLength(200).WithMessage("Ad 200 simvoldan uzun ola bilməz");

        RuleFor(x => x.Position)
            .NotEmpty().WithMessage("Vəzifə boş ola bilməz")
            .MaximumLength(100).WithMessage("Vəzifə 100 simvoldan uzun ola bilməz");

        RuleFor(x => x.MonthlySalary)
            .GreaterThanOrEqualTo(0).WithMessage("Maaş mənfi ola bilməz");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Qeyd 500 simvoldan uzun ola bilməz");
    }
}
