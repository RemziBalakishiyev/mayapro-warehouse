namespace MayaPro.WarehouseApi.Modules.Products.Domain;

/// <summary>
/// The batch-expense buckets that make up a product's real cost. Mirrors the frontend
/// <c>ExpenseBreakdown</c> keys (<c>yol/fehle/yer/paket/diger</c>). The Expenses module maps its own
/// categories onto these kinds when it attaches a cost to a product via the cross-module contract.
/// </summary>
public enum ProductExpenseKind
{
    /// <summary>Freight / transport (wire key <c>yol</c>).</summary>
    Transport = 1,

    /// <summary>Labour / loading (wire key <c>fehle</c>).</summary>
    Labor = 2,

    /// <summary>Storage / place (wire key <c>yer</c>).</summary>
    Storage = 3,

    /// <summary>Packaging (wire key <c>paket</c>).</summary>
    Packaging = 4,

    /// <summary>Other (wire key <c>diger</c>).</summary>
    Other = 5
}
