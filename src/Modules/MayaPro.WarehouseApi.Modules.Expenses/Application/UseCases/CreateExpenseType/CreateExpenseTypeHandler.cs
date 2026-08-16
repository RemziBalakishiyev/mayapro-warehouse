using FluentValidation;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpenseType;

/// <summary>
/// Creates a managed expense type. Rejects an empty/over-long name (validator) or a duplicate one
/// (<see cref="ExpenseErrors.ExpenseTypeDuplicate"/>). The duplicate check is explicitly case-insensitive
/// ("Yol pulu" == "yol pulu", AC-2) rather than relying on the database collation, so the rule holds on any
/// server and is provable in tests. The unique index on Name remains the last-resort guard.
/// </summary>
public sealed class CreateExpenseTypeHandler(
    IExpensesDbContext db,
    IValidator<CreateExpenseTypeCommand> validator)
{
    public async Task<Result<ExpenseTypeDto>> Handle(CreateExpenseTypeCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<ExpenseTypeDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        string name = command.Name.Trim();

        // BE#43 — the duplicate check used to compare `name.ToLower()` (C#, host culture) against
        // `t.Name.ToLower()` translated by EF into SQL Server's LOWER() (collation-based). The host culture
        // is az-Latn-AZ, where 'I'.ToLower() is 'ı' (U+0131), while SQL Server's LOWER() maps 'I' to 'i'.
        // The two sides therefore disagreed for any name containing a Latin 'I' ("INTERNET" vs "internet"
        // never matched) and the duplicate check silently let the second one through.
        //
        // ToLowerInvariant() fixes the C# side, but it cannot simply replace the query-side ToLower(): the
        // SqlServer provider does not translate ToLowerInvariant() to SQL and would throw at run time. The
        // pick-list holds only a few dozen rows (same trade-off the original code already accepted by giving
        // up the index seek), so the comparison is done entirely in memory instead — one round trip to fetch
        // the existing names, then an invariant, culture-independent comparison in C# on both sides. This
        // keeps SQL Server and the in-memory test provider in agreement regardless of host culture.
        string normalized = name.ToLowerInvariant();

        List<string> existingNames = await db.ExpenseTypes.Select(t => t.Name).ToListAsync(ct);
        bool exists = existingNames.Any(existing => existing.ToLowerInvariant() == normalized);
        if (exists)
            return Result.Failure<ExpenseTypeDto>(ExpenseErrors.ExpenseTypeDuplicate);

        var expenseType = ExpenseType.Create(name);
        db.ExpenseTypes.Add(expenseType);
        await db.SaveChangesAsync(ct);

        return Result.Success(new ExpenseTypeDto(expenseType.Id, expenseType.Name));
    }
}
