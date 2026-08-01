using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetProductsKpi;

/// <summary>
/// BE#27 — fetches everything the products-kpi endpoint needs from the Products and Sales read contracts
/// and hands it to the pure <see cref="ProductsKpiCalculator"/>. An empty <c>from</c>/<c>to</c> means the
/// whole history (unbounded); a reversed range (<c>from &gt; to</c>) is rejected rather than coerced.
/// </summary>
public sealed class GetProductsKpiHandler(IProductsModule products, ISalesModule sales, IDateProvider dateProvider)
{
    public async Task<Result<ProductsKpiDto>> Handle(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (from is { } f && to is { } t && f > t)
            return Result.Failure<ProductsKpiDto>(ReportErrors.InvalidDateRange);

        // Stock health is "as of now" — unbounded regardless of from/to.
        IReadOnlyList<ProductSnapshot> snapshots = await products.GetAllSnapshotsAsync(ct);
        IReadOnlyList<SalesReportRow> salesInPeriod = await sales.GetSalesAsync(from, to, ct);
        IReadOnlyList<StockAdjustmentRow> adjustmentsInPeriod =
            await products.GetStockAdjustmentsAsync(from, to, ct);

        // New products' opening stock within the window: CreatedAt is a UTC instant, so the comparison
        // uses the same business-zone day boundaries every other date-ranged contract uses.
        DateTime? fromUtc = from is { } wf ? dateProvider.LocalDayRangeUtc(wf).StartUtc : null;
        DateTime? toUtcExclusive = to is { } wt ? dateProvider.LocalDayRangeUtc(wt).EndUtc : null;
        int newProductUnits = snapshots
            .Where(p =>
                (fromUtc is null || p.CreatedAt >= fromUtc) &&
                (toUtcExclusive is null || p.CreatedAt < toUtcExclusive))
            .Sum(p => p.InitialQuantity);

        return Result.Success(
            ProductsKpiCalculator.Build(snapshots, salesInPeriod, adjustmentsInPeriod, newProductUnits));
    }
}
