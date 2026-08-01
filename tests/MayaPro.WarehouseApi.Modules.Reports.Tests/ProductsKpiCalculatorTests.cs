using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetProductsKpi;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Tests;

/// <summary>
/// Unit tests for the pure <see cref="ProductsKpiCalculator"/> (BE#27, PK-U1..PK-U6) — all figures from
/// in-memory inputs, no database.
/// </summary>
public sealed class ProductsKpiCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 8, 2);

    private static ProductSnapshot Snap(int qty, int min, decimal realCost, decimal salePrice) =>
        new(Guid.NewGuid(), "P", "Cat", qty, min, realCost, salePrice);

    private static SalesReportRow Sale(int qty, Guid? productId = null) =>
        new(Today, TotalAmount: qty * 10m, Profit: qty * 4m, WireFormat.PaymentTypes.Cash,
            productId, "P", qty, UnitPrice: 10m, IsManual: productId is null);

    private static StockAdjustmentRow Adjustment(int delta) => new(Guid.NewGuid(), delta, Today);

    [Fact]
    public void PK_U1_Happy_Path_Computes_Snapshot_Totals()
    {
        var snapshots = new List<ProductSnapshot>
        {
            Snap(qty: 10, min: 2, realCost: 5, salePrice: 8),
            Snap(qty: 1, min: 5, realCost: 3, salePrice: 6),
        };

        ProductsKpiDto dto = ProductsKpiCalculator.Build(snapshots, [], [], newProductUnitsInPeriod: 0);

        Assert.Equal(2, dto.ProductCount);
        Assert.Equal(11, dto.TotalStockUnits);
        Assert.Equal(53m, dto.TotalCostValue);   // 10*5 + 1*3
        Assert.Equal(86m, dto.TotalSaleValue);   // 10*8 + 1*6
        Assert.Equal(33m, dto.PotentialProfit);  // 86 - 53
        Assert.Equal(1, dto.LowStockCount);      // only the qty=1/min=5 row
    }

    [Fact]
    public void PK_U2_Empty_Warehouse_Is_All_Zeros_No_Throw()
    {
        ProductsKpiDto dto = ProductsKpiCalculator.Build([], [], [], newProductUnitsInPeriod: 0);

        Assert.Equal(0, dto.ProductCount);
        Assert.Equal(0, dto.TotalStockUnits);
        Assert.Equal(0m, dto.TotalCostValue);
        Assert.Equal(0m, dto.TotalSaleValue);
        Assert.Equal(0m, dto.PotentialProfit);
        Assert.Equal(0, dto.LowStockCount);
        Assert.Equal(0, dto.SoldUnits);
        Assert.Equal(0, dto.PurchasedUnits);
    }

    [Fact]
    public void PK_U3_Out_Of_Stock_Is_Not_Counted_As_Low_Stock()
    {
        var snapshots = new List<ProductSnapshot> { Snap(qty: 0, min: 5, realCost: 1, salePrice: 2) };

        ProductsKpiDto dto = ProductsKpiCalculator.Build(snapshots, [], [], newProductUnitsInPeriod: 0);

        Assert.Equal(0, dto.LowStockCount);
    }

    [Fact]
    public void PK_U4_SoldUnits_Includes_Free_Form_Sales()
    {
        var sales = new List<SalesReportRow> { Sale(qty: 3, productId: Guid.NewGuid()), Sale(qty: 2, productId: null) };

        ProductsKpiDto dto = ProductsKpiCalculator.Build([], sales, [], newProductUnitsInPeriod: 0);

        Assert.Equal(5, dto.SoldUnits);
    }

    [Fact]
    public void PK_U5_PurchasedUnits_Combines_New_Product_Units_And_Positive_Adjustments()
    {
        var adjustments = new List<StockAdjustmentRow> { Adjustment(15) };

        ProductsKpiDto dto = ProductsKpiCalculator.Build([], [], adjustments, newProductUnitsInPeriod: 20);

        Assert.Equal(35, dto.PurchasedUnits); // 20 (new product) + 15 (adjustment)
    }

    [Fact]
    public void PK_U6_Negative_Adjustments_Are_Excluded_From_PurchasedUnits()
    {
        var adjustments = new List<StockAdjustmentRow> { Adjustment(-5) };

        ProductsKpiDto dto = ProductsKpiCalculator.Build([], [], adjustments, newProductUnitsInPeriod: 0);

        Assert.Equal(0, dto.PurchasedUnits); // the -5 loss/damage correction never counts as stock coming in
    }

    [Fact]
    public void PK_AC2_Snapshot_Fields_Are_Unaffected_By_Period_Inputs()
    {
        var snapshots = new List<ProductSnapshot> { Snap(qty: 10, min: 2, realCost: 5, salePrice: 8) };

        ProductsKpiDto withoutPeriod = ProductsKpiCalculator.Build(snapshots, [], [], newProductUnitsInPeriod: 0);
        ProductsKpiDto withPeriod = ProductsKpiCalculator.Build(
            snapshots, [Sale(qty: 3, productId: Guid.NewGuid())], [Adjustment(7)], newProductUnitsInPeriod: 4);

        Assert.Equal(withoutPeriod.ProductCount, withPeriod.ProductCount);
        Assert.Equal(withoutPeriod.TotalStockUnits, withPeriod.TotalStockUnits);
        Assert.Equal(withoutPeriod.TotalCostValue, withPeriod.TotalCostValue);
        Assert.Equal(withoutPeriod.TotalSaleValue, withPeriod.TotalSaleValue);
        Assert.Equal(withoutPeriod.PotentialProfit, withPeriod.PotentialProfit);
        Assert.Equal(withoutPeriod.LowStockCount, withPeriod.LowStockCount);
        // …only the period-scoped fields differ.
        Assert.Equal(3, withPeriod.SoldUnits);
        Assert.Equal(11, withPeriod.PurchasedUnits);
    }
}
