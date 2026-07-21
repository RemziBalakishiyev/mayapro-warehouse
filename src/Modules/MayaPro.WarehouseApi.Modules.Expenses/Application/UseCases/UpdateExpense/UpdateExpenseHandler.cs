using FluentValidation;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.UpdateExpense;

/// <summary>
/// Revises an expense by reverse-and-reapply, all in one transaction: the old expense's product-cost effect
/// is unwound (best-effort), then the <c>CreateExpense</c> chain is applied afresh with the new values on the
/// same row — its identity and creator are preserved. A new product link is validated (must exist → else the
/// whole update rolls back). Guarded by the closed-day rule for both the expense's current day and, when the
/// date changes, its new day: an expense cannot be edited into or out of a closed day.
/// </summary>
public sealed class UpdateExpenseHandler(
    IExpensesDbContext db,
    IUnitOfWork unitOfWork,
    IProductsModule products,
    IDayEndModule dayEnd,
    IValidator<UpdateExpenseCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser,
    IDateProvider dateProvider)
{
    public async Task<Result<ExpenseDto>> Handle(UpdateExpenseCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<ExpenseDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        Expense? expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == command.Id, ct);
        if (expense is null)
            return Result.Failure<ExpenseDto>(ExpenseErrors.NotFound);

        // Validated above, so this always succeeds.
        ExpenseCategoryCode.TryParse(command.Category, out ExpenseCategory category);
        DateTime date = command.Date ?? expense.Date;

        // Neither the expense's current day nor (if it moves) its new day may be closed.
        if (await dayEnd.ClosingExistsAsync(dateProvider.ToLocalDate(expense.Date), ct))
            return Result.Failure<ExpenseDto>(ExpenseErrors.DayClosedConflict);
        if (dateProvider.ToLocalDate(date) != dateProvider.ToLocalDate(expense.Date) &&
            await dayEnd.ClosingExistsAsync(dateProvider.ToLocalDate(date), ct))
            return Result.Failure<ExpenseDto>(ExpenseErrors.DayClosedConflict);

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        // ① Reverse the old product-cost effect (best-effort — a since-deleted product has nothing to unwind).
        if (expense.ProductId is { } oldProductId)
            await products.RemoveExpenseFromProductAsync(oldProductId, expense.Category.ToCode(), expense.Amount, ct);

        // ② Reapply with the new values — same chain as CreateExpense; a bad product link rolls the update back.
        string? productName = null;
        if (command.ProductId is { } productId)
        {
            Result<ProductSnapshot> snapshot = await products.GetSnapshotAsync(productId, ct);
            if (snapshot.IsFailure)
                return Result.Failure<ExpenseDto>(snapshot.Error);

            productName = snapshot.Value.Name;

            Result attach = await products.AddExpenseToProductAsync(
                productId, category.ToCode(), command.Amount, ct);
            if (attach.IsFailure)
                return Result.Failure<ExpenseDto>(attach.Error);
        }

        expense.Update(command.Title, category, command.Amount, date, command.ProductId, productName, command.Note);

        await activityLogger.LogAsync(
            "Xərci düzəltdi",
            $"{expense.Name} — {expense.Amount:0.00} AZN",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(expense.ToDto());
    }
}
