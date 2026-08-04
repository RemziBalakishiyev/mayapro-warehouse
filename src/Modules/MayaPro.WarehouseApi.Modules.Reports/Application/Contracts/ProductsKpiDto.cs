namespace MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;

/// <summary>
/// BE#27 — <c>GET /api/reports/products-kpi?from=&amp;to=</c>. Stock health fields (<see cref="ProductCount"/>
/// through <see cref="LowStockCount"/>) are the current snapshot and never move with <c>from</c>/<c>to</c> —
/// stock is an "as of now" fact, not a period total. Only <see cref="SoldUnits"/> and
/// <see cref="PurchasedUnits"/> are period-scoped: units sold in the window, and units added to stock in it
/// (new products' opening quantity plus positive manual stock corrections).
/// </summary>
public sealed record ProductsKpiDto(
    int ProductCount,
    int TotalStockUnits,
    decimal TotalCostValue,
    decimal TotalSaleValue,
    decimal PotentialProfit,
    int LowStockCount,
    int SoldUnits,
    int PurchasedUnits);
