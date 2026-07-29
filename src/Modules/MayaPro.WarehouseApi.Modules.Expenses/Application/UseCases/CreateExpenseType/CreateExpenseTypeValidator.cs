using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpenseType;

public sealed class CreateExpenseTypeValidator : AbstractValidator<CreateExpenseTypeCommand>
{
    public CreateExpenseTypeValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Xərc növü adı boş ola bilməz")
            // Matches ExpenseTypes.Name (nvarchar(100)) — without this a longer name reaches the database
            // and fails there with a 500 instead of a 400.
            .MaximumLength(100).WithMessage("Xərc növü adı 100 simvoldan uzun ola bilməz");
    }
}
