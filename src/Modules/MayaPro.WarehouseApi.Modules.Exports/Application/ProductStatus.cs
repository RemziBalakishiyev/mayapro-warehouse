namespace MayaPro.WarehouseApi.Modules.Exports.Application;

/// <summary>
/// Stock status labels matching the frontend <c>ProductStatus</c> stock triad
/// (Stokda var / Azalır / Bitib). Sale-vs-cost statuses (Satılmır / Ziyana satılır) are not used on export.
/// </summary>
internal static class ProductStatus
{
    public const string InStock = "Stokda var";
    public const string Low = "Azalır";
    public const string Out = "Bitib";

    public static string FromQuantity(int quantity, int minStock)
    {
        if (quantity <= 0)
            return Out;
        if (quantity <= minStock)
            return Low;
        return InStock;
    }
}
