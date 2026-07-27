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

        // The date is a UTC instant, but "future" is judged on the business calendar (ADR-0005):
        // 20:00 UTC is already the next Baku day. An omitted date keeps the current one, so nothing to check.
        RuleFor(x => x.Date)
            .Must(date => dateProvider.ToLocalDate(date!.Value) <= dateProvider.Today)
            .When(x => x.Date is not null)
            .WithMessage("Xərcin tarixi gələcək ola bilməz");
    }
}
