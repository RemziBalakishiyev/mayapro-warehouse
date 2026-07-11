using MayaPro.WarehouseApi.Modules.Products.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Infrastructure;

/// <summary>
/// Development seeder: if the Products table is empty, inserts the ten demo products from the frontend
/// <c>seed.ts</c> with identical values. Real cost is computed by the domain (never seeded literally).
/// Each product is created at its initial quantity — which fixes the real-cost divisor — then stock is
/// adjusted down to the current on-hand quantity, exactly as the frontend seed models sales-to-date.
/// </summary>
public sealed class ProductSeeder(ProductsDbContext db)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Products.AnyAsync(ct))
            return;

        for (int i = 0; i < RawProducts.Length; i++)
        {
            RawProduct raw = RawProducts[i];
            (string store, string warehouse, string shelf, string box) = ParseLocation(raw.Location);

            var product = Product.Create(
                raw.Name,
                raw.Category,
                raw.Size,
                raw.Color,
                raw.Model,
                $"SDK{1000 + i + 1}",
                image: string.Empty,
                note: string.Empty,
                raw.PurchasePrice,
                raw.SalePrice,
                raw.InitialQuantity,
                raw.MinStock,
                currency: "AZN",
                raw.SupplierId,
                raw.Location,
                store,
                warehouse,
                shelf,
                box,
                new ProductExpenses(raw.Transport, raw.Labor, raw.Storage, raw.Packaging, raw.Other));

            // Bring stock down from the initial batch to what is currently on hand (sales to date).
            if (raw.Quantity != raw.InitialQuantity)
                product.AdjustStock(raw.Quantity - raw.InitialQuantity);

            db.Products.Add(product);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Splits "Anbar A / Rəf 3 / Qutu 12" into its parts, mirroring the frontend parseLocation.</summary>
    private static (string Store, string Warehouse, string Shelf, string Box) ParseLocation(string location)
    {
        string[] parts = location.Split(" / ");
        string store = parts.Length > 0 ? parts[0] : string.Empty;
        string shelfPart = parts.Length > 1 ? parts[1] : string.Empty;
        string boxPart = parts.Length > 2 ? parts[2] : string.Empty;

        return (store, store, shelfPart.Replace("Rəf ", string.Empty), boxPart.Replace("Qutu ", string.Empty));
    }

    private sealed record RawProduct(
        string Name,
        string Category,
        string Size,
        string Color,
        string Model,
        decimal PurchasePrice,
        decimal SalePrice,
        int Quantity,
        int InitialQuantity,
        int MinStock,
        string SupplierId,
        string Location,
        decimal Transport,
        decimal Labor,
        decimal Storage,
        decimal Packaging,
        decimal Other);

    private static readonly RawProduct[] RawProducts =
    [
        new("Kişi cins şalvar Slim", "Şalvar", "30-38", "Tünd göy", "MNG-armani",
            14, 25, 84, 120, 20, "sup_4", "Anbar A / Rəf 3 / Qutu 12", 240, 60, 50, 30, 0),
        new("Qadın bluz ipək", "Bluz", "S-XL", "Bej", "Zara style",
            8, 18, 12, 80, 15, "sup_1", "Anbar A / Rəf 1 / Qutu 4", 160, 40, 30, 20, 0),
        new("İdman ayaqqabısı AirMax", "Ayaqqabı", "40-45", "Qara/Ağ", "N-Air replika",
            22, 45, 46, 60, 10, "sup_2", "Anbar B / Rəf 2 / Qutu 7", 300, 60, 60, 40, 20),
        new("Uşaq kombinzon qış", "Uşaq geyimi", "2-7 yaş", "Qırmızı", "WinterKids",
            16, 32, 0, 40, 8, "sup_1", "Anbar A / Rəf 5 / Qutu 2", 120, 30, 20, 10, 0),
        new("Qadın çanta dəri", "Aksesuar", "Standart", "Qəhvəyi", "LV style",
            12, 28, 34, 50, 10, "sup_3", "Mağaza / Vitrin 1", 90, 25, 20, 15, 0),
        new("Kişi köynək klassik", "Köynək", "M-XXL", "Ağ", "Classic-FIT",
            9, 17, 95, 100, 20, "sup_1", "Anbar A / Rəf 2 / Qutu 9", 180, 45, 30, 25, 0),
        new("Qış gödəkçəsi kişi", "Gödəkçə", "L-XXL", "Qara", "NorthStyle",
            35, 33, 28, 35, 6, "sup_1", "Anbar B / Rəf 4 / Qutu 1", 200, 50, 40, 30, 0),
        new("Qadın idman dəsti", "İdman", "S-L", "Çəhrayı", "FitSet",
            13, 27, 8, 45, 10, "sup_4", "Anbar A / Rəf 6 / Qutu 3", 110, 30, 25, 15, 0),
        new("Uşaq krossovka LED", "Ayaqqabı", "25-34", "Göy", "KidsLight",
            10, 22, 52, 55, 12, "sup_2", "Anbar B / Rəf 1 / Qutu 5", 140, 35, 25, 20, 0),
        new("Kəmər dəri kişi", "Aksesuar", "Universal", "Qara", "BeltPro",
            4, 10, 140, 150, 30, "sup_3", "Mağaza / Vitrin 2", 45, 15, 10, 10, 0)
    ];
}
