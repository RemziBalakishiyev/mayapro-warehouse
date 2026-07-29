namespace MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;

/// <summary>A managed expense type as returned by the API.</summary>
public sealed record ExpenseTypeDto(Guid Id, string Name);
