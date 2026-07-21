namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.UpdateExpense;

/// <summary>
/// Input for revising an expense. Same shape as creating one plus the <see cref="Id"/> from the route. When
/// <see cref="Date"/> is omitted the expense keeps its current date. Changing the amount or the linked
/// <see cref="ProductId"/> re-runs the product real-cost chain (reverse the old, apply the new).
/// </summary>
public sealed record UpdateExpenseCommand(
    Guid Id,
    string Title,
    string Category,
    decimal Amount,
    DateTime? Date,
    Guid? ProductId,
    string? Note);
