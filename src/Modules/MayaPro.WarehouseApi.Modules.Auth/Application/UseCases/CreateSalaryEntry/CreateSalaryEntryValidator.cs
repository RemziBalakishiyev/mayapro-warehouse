using FluentValidation;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.CreateSalaryEntry;

public sealed class CreateSalaryEntryValidator : AbstractValidator<CreateSalaryEntryCommand>
{
    public CreateSalaryEntryValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Məbləğ sıfırdan böyük olmalıdır");

        // Matches the SalaryEntries.Note column (nvarchar(500)) — a longer note would otherwise fail in the
        // database (500) instead of here (400).
        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Qeyd 500 simvoldan uzun ola bilməz");
    }
}
