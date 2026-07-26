using FluentValidation;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpenseType;

/// <summary>
/// Creates a managed expense type. Rejects an empty name (validator) or a duplicate one
/// (<see cref="ExpenseErrors.ExpenseTypeDuplicate"/>). The comparison is case-insensitive, matching the
/// unique index under SQL Server's default collation.
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

        bool exists = await db.ExpenseTypes.AnyAsync(t => t.Name == name, ct);
        if (exists)
            return Result.Failure<ExpenseTypeDto>(ExpenseErrors.ExpenseTypeDuplicate);

        var expenseType = ExpenseType.Create(name);
        db.ExpenseTypes.Add(expenseType);
        await db.SaveChangesAsync(ct);

        return Result.Success(new ExpenseTypeDto(expenseType.Id, expenseType.Name));
    }
}
