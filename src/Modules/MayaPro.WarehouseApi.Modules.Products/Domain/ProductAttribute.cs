namespace MayaPro.WarehouseApi.Modules.Products.Domain;

/// <summary>
/// A single dynamic product attribute — a free-form (name, value) pair such as ("Ölçü", "30-38") or
/// ("Rəng", "Qara"). Replaces the old fixed Size/Color/Model columns: products may now carry any number of
/// attributes. Persisted as a JSON array on the <see cref="Product"/> row, not as a separate table.
/// </summary>
public sealed record ProductAttribute(string Name, string Value);
