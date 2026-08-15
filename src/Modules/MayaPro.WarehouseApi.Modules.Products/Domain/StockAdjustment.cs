using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Products.Domain;

/// <summary>
/// A dated record of a manual stock correction (see <see cref="Product.AdjustStock"/>) — the structured,
/// queryable counterpart to the free-text activity-log entry <c>AdjustStockHandler</c> also writes. Kept as
/// its own tiny entity (rather than reconstructing history from <see cref="Product.Quantity"/>, which only
/// carries the current state) so the Reports module's KPIs (BE#27) can ask "how much stock was added or
/// removed in this period" via <see cref="MayaPro.WarehouseApi.SharedKernel.Contracts.IProductsModule.GetStockAdjustmentsAsync"/>
/// without touching the products table's current-state columns.
/// </summary>
public sealed class StockAdjustment : TenantEntity
{
    // EF Core constructor.
    private StockAdjustment() { }

    private StockAdjustment(Guid productId, int delta, DateTime date)
    {
        ProductId = productId;
        Delta = delta;
        Date = date;
    }

    public Guid ProductId { get; private set; }

    /// <summary>Signed correction applied to the product's stock — positive adds, negative removes.</summary>
    public int Delta { get; private set; }

    public DateTime Date { get; private set; }

    public static StockAdjustment Create(Guid productId, int delta) => new(productId, delta, DateTime.UtcNow);
}
