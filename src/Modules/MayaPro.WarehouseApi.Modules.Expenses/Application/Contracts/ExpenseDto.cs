namespace MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;

/// <summary>
/// An expense as returned by the API. <c>title</c> is the expense name and <c>category</c> is a free-form
/// snapshot of the chosen expense type's name. <c>source</c> is <c>"general"</c> or <c>"product"</c>
/// (see <see cref="SharedKernel.Contracts.WireFormat.ExpenseSources"/>).
/// </summary>
public sealed record ExpenseDto(
    Guid Id,
    string Title,
    string Category,
    string Source,
    decimal Amount,
    DateTime Date,
    Guid? ProductId,
    string? ProductName,
    string? Note,
    Guid? CreatedByUserId,
    DateTime CreatedAt);
