using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.SetEmployeeSalary;

public sealed class SetEmployeeSalaryValidator : AbstractValidator<SetEmployeeSalaryCommand>
{
    public SetEmployeeSalaryValidator()
    {
        // Zero is legitimate ("no salary agreed yet"); negative never is.
        RuleFor(x => x.MonthlySalary)
            .GreaterThanOrEqualTo(0).WithMessage("Maaş mənfi ola bilməz");
    }
}
