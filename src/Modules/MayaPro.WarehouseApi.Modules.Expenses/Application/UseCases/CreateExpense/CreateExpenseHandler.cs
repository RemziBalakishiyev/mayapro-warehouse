using FluentValidation;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpense;

/// <summary>
/// Records an expense. Core rule: a <c>product</c>-sourced expense is added to that product's cost bucket
/// so its real cost is recomputed; a <c>general</c> expense (no product) never touches any product's cost
/// — the whole thing in one transaction. In order: begin transaction, resolve the product snapshot (fails +
/// rolls back if it does not exist), raise the product's cost, write the expense (with a product-name
/// snapshot), log the activity, save and commit.
/// </summary>
public sealed class CreateExpenseHandler(
    IExpensesDbContext db,
    IUnitOfWork unitOfWork,
    IProductsModule products,
    IValidator<CreateExpenseCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<ExpenseDto>> Handle(CreateExpenseCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<ExpenseDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        // Validated above, so this always succeeds.
        ExpenseSourceCode.TryParse(command.Source, out ExpenseSource source);
        DateTime date = command.Date ?? DateTime.UtcNow;

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        string? productName = null;
        if (source == ExpenseSource.Product)
        {
            // Validated above: a product-sourced expense always carries a ProductId.
            Guid productId = command.ProductId!.Value;

            // Snapshot the product name (also proves it exists → not found rolls back).
            Result<ProductSnapshot> snapshot = await products.GetSnapshotAsync(productId, ct);
            if (snapshot.IsFailure)
                return Result.Failure<ExpenseDto>(snapshot.Error);

            productName = snapshot.Value.Name;

            // Core rule: raise the product's real cost — the expense type name becomes the free-form line name.
            Result attach = await products.AddExpenseToProductAsync(
                productId, command.Category, command.Amount, ct);
            if (attach.IsFailure)
                return Result.Failure<ExpenseDto>(attach.Error);
        }

        var expense = Expense.Create(
            command.Title,
            command.Category,
            source,
            command.Amount,
            date,
            command.ProductId,
            productName,
            command.Note,
            currentUser.UserId);

        db.Expenses.Add(expense);

        await activityLogger.LogAsync(
            "Xərc əlavə etdi",
            $"{expense.Name} — {expense.Amount:0.00} AZN",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(expense.ToDto());
    }
}
