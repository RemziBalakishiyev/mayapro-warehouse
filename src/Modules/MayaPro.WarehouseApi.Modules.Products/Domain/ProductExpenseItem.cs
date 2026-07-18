namespace MayaPro.WarehouseApi.Modules.Products.Domain;

/// <summary>
/// A single free-form batch expense line — a (name, amount) pair such as ("Yol pulu", 240) or
/// ("Fəhlə", 100). Replaces the old fixed Transport/Labor/Storage/Packaging/Other buckets: products may
/// now carry any number of named expense lines. Persisted as a JSON array on the <see cref="Product"/>
/// row, not as a separate table.
/// </summary>
public sealed record ProductExpenseItem(string Name, decimal Amount);
