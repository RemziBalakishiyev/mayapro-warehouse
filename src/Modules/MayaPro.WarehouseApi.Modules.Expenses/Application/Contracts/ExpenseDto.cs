namespace MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;

/// <summary>
/// An expense as returned by the API. <c>title</c> is the expense name and <c>category</c> is the
/// frontend category code — matching the frontend <c>Expense</c> type.
/// </summary>
public sealed record ExpenseDto(
    Guid Id,
    string Title,
    string Category,
    decimal Amount,
    DateTime Date,
    Guid? ProductId,
    string? ProductName,
    string? Note,
    Guid? CreatedByUserId,
    DateTime CreatedAt);
