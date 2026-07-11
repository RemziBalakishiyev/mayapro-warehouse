namespace MayaPro.WarehouseApi.Modules.Products.Domain;

/// <summary>
/// The per-batch expense breakdown owned by a <see cref="Product"/> (EF <c>OwnsOne</c>: columns
/// <c>Expenses_Transport</c>, <c>Expenses_Labor</c>...). All amounts are AZN. Behaviour-rich: expenses are
/// only ever added through <see cref="Add"/>, never set arbitrarily from outside.
/// </summary>
public sealed class ProductExpenses
{
    // EF Core constructor.
    private ProductExpenses() { }

    public ProductExpenses(decimal transport, decimal labor, decimal storage, decimal packaging, decimal other)
    {
        Transport = transport;
        Labor = labor;
        Storage = storage;
        Packaging = packaging;
        Other = other;
    }

    public decimal Transport { get; private set; }

    public decimal Labor { get; private set; }

    public decimal Storage { get; private set; }

    public decimal Packaging { get; private set; }

    public decimal Other { get; private set; }

    /// <summary>Sum of every bucket — the total batch expense spread across the product's units.</summary>
    public decimal Total => Transport + Labor + Storage + Packaging + Other;

    public static ProductExpenses Empty() => new(0, 0, 0, 0, 0);

    /// <summary>Adds <paramref name="amount"/> to the given bucket, returning nothing (mutates in place).</summary>
    internal void Add(ProductExpenseKind kind, decimal amount)
    {
        switch (kind)
        {
            case ProductExpenseKind.Transport:
                Transport += amount;
                break;
            case ProductExpenseKind.Labor:
                Labor += amount;
                break;
            case ProductExpenseKind.Storage:
                Storage += amount;
                break;
            case ProductExpenseKind.Packaging:
                Packaging += amount;
                break;
            case ProductExpenseKind.Other:
                Other += amount;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Naməlum xərc növü");
        }
    }
}
