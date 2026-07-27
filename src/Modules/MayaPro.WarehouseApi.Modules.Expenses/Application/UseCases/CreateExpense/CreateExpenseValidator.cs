using FluentValidation;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpense;

public sealed class CreateExpenseValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseValidator(IDateProvider dateProvider)
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Ad boş ola bilməz");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Məbləğ sıfırdan böyük olmalıdır");

        RuleFor(x => x.Category)
            .Must(code => ExpenseCategoryCode.TryParse(code, out _))
            .WithMessage("Xərc kateqoriyası yanlışdır");

        // The date is a UTC instant, but "future" is judged on the business calendar (ADR-0005):
        // 20:00 UTC is already the next Baku day. An omitted date means "now", so there is nothing to check.
        RuleFor(x => x.Date)
            .Must(date => dateProvider.ToLocalDate(date!.Value) <= dateProvider.Today)
            .When(x => x.Date is not null)
            .WithMessage("Xərcin tarixi gələcək ola bilməz");
    }
}
