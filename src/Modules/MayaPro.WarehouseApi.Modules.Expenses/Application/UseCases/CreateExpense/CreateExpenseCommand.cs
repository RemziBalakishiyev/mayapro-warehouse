namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpense;

/// <summary>
/// Input for creating an expense. <see cref="Category"/> is a free-form snapshot of the chosen expense
/// type's name. <see cref="Source"/> is <c>"general"</c> or <c>"product"</c> and must agree with
/// <see cref="ProductId"/>: <c>"product"</c> requires it, <c>"general"</c> forbids it. <see cref="Date"/>
/// defaults to now if omitted.
/// </summary>
public sealed record CreateExpenseCommand(
    string Title,
    string Category,
    string Source,
    decimal Amount,
    DateTime? Date,
    Guid? ProductId,
    string? Note);
