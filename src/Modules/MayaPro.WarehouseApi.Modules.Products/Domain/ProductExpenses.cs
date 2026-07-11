namespace MayaPro.WarehouseApi.Modules.Products.Domain;

/// <summary>
/// The per-batch expense breakdown owned by a <see cref="Product"/> (EF <c>OwnsOne</c>: columns
/// <c>Expenses_Yol</c>, <c>Expenses_Fehle</c>...). All amounts are AZN. Behaviour-rich: expenses are
/// only ever added through <see cref="Add"/>, never set arbitrarily from outside.
/// </summary>
public sealed class ProductExpenses
{
    // EF Core constructor.
    private ProductExpenses() { }

    public ProductExpenses(decimal yol, decimal fehle, decimal yer, decimal paket, decimal diger)
    {
        Yol = yol;
        Fehle = fehle;
        Yer = yer;
        Paket = paket;
        Diger = diger;
    }

    public decimal Yol { get; private set; }

    public decimal Fehle { get; private set; }

    public decimal Yer { get; private set; }

    public decimal Paket { get; private set; }

    public decimal Diger { get; private set; }

    /// <summary>Sum of every bucket — the total batch expense spread across the product's units.</summary>
    public decimal Total => Yol + Fehle + Yer + Paket + Diger;

    public static ProductExpenses Empty() => new(0, 0, 0, 0, 0);

    /// <summary>Adds <paramref name="amount"/> to the given bucket, returning nothing (mutates in place).</summary>
    internal void Add(ProductExpenseKind kind, decimal amount)
    {
        switch (kind)
        {
            case ProductExpenseKind.Yol:
                Yol += amount;
                break;
            case ProductExpenseKind.Fehle:
                Fehle += amount;
                break;
            case ProductExpenseKind.Yer:
                Yer += amount;
                break;
            case ProductExpenseKind.Paket:
                Paket += amount;
                break;
            case ProductExpenseKind.Diger:
                Diger += amount;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Naməlum xərc növü");
        }
    }
}
