namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpense;

/// <summary>
/// Input for creating an expense. <see cref="Category"/> is a frontend code (EXP_CATS). <see cref="ProductId"/>
/// attaches the expense to a product (raising its real cost). <see cref="Date"/> defaults to now if omitted.
/// </summary>
public sealed record CreateExpenseCommand(
    string Title,
    string Category,
    decimal Amount,
    DateTime? Date,
    Guid? ProductId,
    string? Note);
