using FluentValidation;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpense;

public sealed class CreateExpenseValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Ad boş ola bilməz");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Məbləğ sıfırdan böyük olmalıdır");

        RuleFor(x => x.Category)
            .Must(code => ExpenseCategoryCode.TryParse(code, out _))
            .WithMessage("Xərc kateqoriyası yanlışdır");
    }
}
