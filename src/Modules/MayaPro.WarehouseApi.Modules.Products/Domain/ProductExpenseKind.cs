namespace MayaPro.WarehouseApi.Modules.Products.Domain;

/// <summary>
/// The batch-expense buckets that make up a product's real cost. Mirrors the frontend
/// <c>ExpenseBreakdown</c> keys (<c>yol/fehle/yer/paket/diger</c>). The Expenses module maps its own
/// categories onto these kinds when it attaches a cost to a product via the cross-module contract.
/// </summary>
public enum ProductExpenseKind
{
    /// <summary>Yol — freight / transport.</summary>
    Yol = 1,

    /// <summary>Fəhlə — labour / loading.</summary>
    Fehle = 2,

    /// <summary>Anbar/Yer — storage / place.</summary>
    Yer = 3,

    /// <summary>Paket/Qutu — packaging.</summary>
    Paket = 4,

    /// <summary>Digər — other.</summary>
    Diger = 5
}
