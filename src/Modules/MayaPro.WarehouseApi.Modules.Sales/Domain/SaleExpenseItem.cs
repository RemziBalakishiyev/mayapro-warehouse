namespace MayaPro.WarehouseApi.Modules.Sales.Domain;

/// <summary>
/// A single free-form expense line recorded against a sale — a (name, amount) pair such as
/// ("Yol pulu", 5) or ("Fəhlə", 3). Only free-form (manual) sales carry these: they document how the
/// seller arrived at the cost they typed in. A catalogued sale takes its cost from the product, so its
/// expense list stays empty. Persisted as a JSON array on the <see cref="Sale"/> row, not a separate table.
/// </summary>
public sealed record SaleExpenseItem(string Name, decimal Amount);
