using MayaPro.WarehouseApi.Modules.Expenses.Domain;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;

/// <summary>Maps the <see cref="Expense"/> entity to its wire DTO (category as the frontend code).</summary>
public static class ExpenseMapping
{
    public static ExpenseDto ToDto(this Expense expense) =>
        new(
            expense.Id,
            expense.Name,
            expense.Category.ToCode(),
            expense.Amount,
            expense.Date,
            expense.ProductId,
            expense.ProductName,
            expense.Note,
            expense.CreatedByUserId,
            expense.CreatedAt);
}
