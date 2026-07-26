using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpenseType;

public sealed class CreateExpenseTypeValidator : AbstractValidator<CreateExpenseTypeCommand>
{
    public CreateExpenseTypeValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Xərc növü adı boş ola bilməz");
    }
}
