using FluentValidation;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.UpdateExpense;

/// <summary>Same rules as creating an expense — an update is a full reverse-and-reapply of its values.</summary>
public sealed class UpdateExpenseValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseValidator(IDateProvider dateProvider)
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Ad boş ola bilməz");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Məbləğ sıfırdan böyük olmalıdır");

        RuleFor(x => x.Category)
            .Must(code => ExpenseCategoryCode.TryParse(code, out _))
            .WithMessage("Xərc kateqoriyası yanlışdır");

        // "Bugün" iş saat qurşağı ilə (Asia/Baku) hesablanır — bax IDateProvider (ADR 0005).
        RuleFor(x => x.Date)
            .Must(date => date is null || dateProvider.ToLocalDate(date.Value) <= dateProvider.Today)
            .WithMessage("Xərcin tarixi gələcək ola bilməz");
    }
}
