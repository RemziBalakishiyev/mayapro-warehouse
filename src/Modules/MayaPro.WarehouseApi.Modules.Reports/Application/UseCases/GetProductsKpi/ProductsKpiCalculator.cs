using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetProductsKpi;

/// <summary>
/// Pure products-KPI maths (BE#27). Takes the raw data already fetched from the other modules plus one
/// number the handler resolves from the products' <c>CreatedAt</c> window (new products' opening stock in
/// the period — see <c>GetProductsKpiHandler</c>), and produces the finished <see cref="ProductsKpiDto"/>.
/// Side-effect free and fully unit-testable without a database.
/// </summary>
public static class ProductsKpiCalculator
{
    public static ProductsKpiDto Build(
        IReadOnlyList<ProductSnapshot> snapshots,
        IReadOnlyList<SalesReportRow> salesInPeriod,
        IReadOnlyList<StockAdjustmentRow> adjustmentsInPeriod,
        int newProductUnitsInPeriod)
    {
        decimal totalCostValue = snapshots.Sum(p => p.Quantity * p.RealCostPerUnit);
        decimal totalSaleValue = snapshots.Sum(p => p.Quantity * p.SalePrice);

        // Units sold include free-form sales (no ProductId) — every unit that left the shelf counts.
        int soldUnits = salesInPeriod.Sum(s => s.Quantity);

        // Purchased/added units: a new product's opening quantity plus positive manual corrections only —
        // a negative correction (loss/damage) never counts as stock coming IN.
        int positiveAdjustments = adjustmentsInPeriod.Where(a => a.Delta > 0).Sum(a => a.Delta);
        int purchasedUnits = newProductUnitsInPeriod + positiveAdjustments;

        return new ProductsKpiDto(
            ProductCount: snapshots.Count,
            TotalStockUnits: snapshots.Sum(p => p.Quantity),
            TotalCostValue: totalCostValue,
            TotalSaleValue: totalSaleValue,
            PotentialProfit: totalSaleValue - totalCostValue,
            LowStockCount: snapshots.Count(IsLowStock),
            SoldUnits: soldUnits,
            PurchasedUnits: purchasedUnits);
    }

    // Same rule as DashboardCalculator.IsLowStock: out-of-stock (0) is not "low stock", it's "no stock".
    private static bool IsLowStock(ProductSnapshot p) => p.Quantity > 0 && p.Quantity <= p.MinStock;
}
