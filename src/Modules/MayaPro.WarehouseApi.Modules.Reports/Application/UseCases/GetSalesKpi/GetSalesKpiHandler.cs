using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSalesKpi;

/// <summary>
/// BE#27 — fetches the period's sales from <see cref="ISalesModule"/> and hands them to the pure
/// <see cref="SalesKpiCalculator"/>. An empty <c>from</c>/<c>to</c> means the whole history (unbounded); a
/// reversed range (<c>from &gt; to</c>) is rejected rather than coerced.
/// </summary>
public sealed class GetSalesKpiHandler(ISalesModule sales)
{
    public async Task<Result<SalesKpiDto>> Handle(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (from is { } f && to is { } t && f > t)
            return Result.Failure<SalesKpiDto>(ReportErrors.InvalidDateRange);

        IReadOnlyList<SalesReportRow> salesInPeriod = await sales.GetSalesAsync(from, to, ct);
        return Result.Success(SalesKpiCalculator.Build(salesInPeriod));
    }
}
