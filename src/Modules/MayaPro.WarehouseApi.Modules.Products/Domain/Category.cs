using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Products.Domain;

/// <summary>
/// A managed product category (its own table in the <c>products</c> schema, unique by name). This is the
/// pick-list the UI offers when adding a product; <see cref="Product.Category"/> stays a plain string
/// snapshot, so renaming or deleting a category never rewrites existing products.
/// </summary>
public sealed class Category : Entity
{
    // EF Core constructor.
    private Category() { }

    private Category(string name) => Name = name;

    public string Name { get; private set; } = string.Empty;

    public static Category Create(string name) => new(name.Trim());
}
