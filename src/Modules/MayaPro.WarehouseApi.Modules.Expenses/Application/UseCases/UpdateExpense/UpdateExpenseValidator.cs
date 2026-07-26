using FluentValidation;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.UpdateExpense;

/// <summary>Same rules as creating an expense — an update is a full reverse-and-reapply of its values.</summary>
public sealed class UpdateExpenseValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Ad boş ola bilməz");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Məbləğ sıfırdan böyük olmalıdır");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Xərc növü boş ola bilməz")
            // Matches the Expenses.Category column (nvarchar(100)) — a longer snapshot would otherwise fail
            // in the database (500) instead of here (400).
            .MaximumLength(100).WithMessage("Xərc növü 100 simvoldan uzun ola bilməz");

        RuleFor(x => x.Source)
            .Must(code => ExpenseSourceCode.TryParse(code, out _))
            .WithMessage("Xərc mənbəyi yanlışdır");

        RuleFor(x => x.ProductId)
            .NotNull().WithMessage("Mala bağlı xərc üçün ProductId tələb olunur")
            .When(x => x.Source == WireFormat.ExpenseSources.Product);

        RuleFor(x => x.ProductId)
            .Null().WithMessage("Ümumi xərc üçün ProductId göndərilməməlidir")
            .When(x => x.Source == WireFormat.ExpenseSources.General);
    }
}
