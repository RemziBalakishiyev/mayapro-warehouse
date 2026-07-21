using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.DeleteExpense;

/// <summary>
/// Deletes an expense, unwinding its effect in one transaction — the reverse of <c>CreateExpense</c>: a
/// product-linked expense subtracts its amount back off that product's real cost (Products contract), then
/// the expense row is removed and the delete is logged. Guarded by the closed-day rule: an expense whose day
/// is already closed cannot be deleted. The cost reversal is best-effort — if the product has since been
/// deleted there is nothing to unwind.
/// </summary>
public sealed class DeleteExpenseHandler(
    IExpensesDbContext db,
    IUnitOfWork unitOfWork,
    IProductsModule products,
    IDayEndModule dayEnd,
    IActivityLogger activityLogger,
    ICurrentUser currentUser,
    IDateProvider dateProvider)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        Expense? expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (expense is null)
            return Result.Failure(ExpenseErrors.NotFound);

        if (await dayEnd.ClosingExistsAsync(dateProvider.ToLocalDate(expense.Date), ct))
            return Result.Failure(ExpenseErrors.DayClosedConflict);

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        // Reverse the product-cost effect (best-effort — the only possible failure is a since-deleted product).
        if (expense.ProductId is { } productId)
            await products.RemoveExpenseFromProductAsync(productId, expense.Category.ToCode(), expense.Amount, ct);

        db.Expenses.Remove(expense);

        await activityLogger.LogAsync(
            "Xərc sildi",
            $"{expense.Name} — {expense.Amount:0.00} AZN",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success();
    }
}
