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
        // Compared lowered on both sides — the same operation on either side keeps SQL Server and the
        // in-memory provider in agreement. The pick-list holds a few dozen rows, so the lost index seek is free.
        string normalized = name.ToLower();

        bool exists = await db.ExpenseTypes.AnyAsync(t => t.Name.ToLower() == normalized, ct);
        if (exists)
            return Result.Failure<ExpenseTypeDto>(ExpenseErrors.ExpenseTypeDuplicate);

        var expenseType = ExpenseType.Create(name);
        db.ExpenseTypes.Add(expenseType);
        await db.SaveChangesAsync(ct);

        return Result.Success(new ExpenseTypeDto(expenseType.Id, expenseType.Name));
    }
}
