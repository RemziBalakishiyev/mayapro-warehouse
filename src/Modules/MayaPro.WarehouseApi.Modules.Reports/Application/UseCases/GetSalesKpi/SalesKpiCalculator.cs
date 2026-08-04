using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSalesKpi;

/// <summary>
/// Pure sales-KPI maths (BE#27): the same revenue/profit/unknown-profit rules <c>GetSummaryHandler</c>
/// applies, plus a fixed Nağd/Kart/Nisyə breakdown. Side-effect free and fully unit-testable.
/// </summary>
public static class SalesKpiCalculator
{
    // Fixed order so byPayment always has exactly three rows, even when a method had no sales.
    private static readonly string[] PaymentTypeOrder =
    {
        WireFormat.PaymentTypes.Cash,
        WireFormat.PaymentTypes.Card,
        WireFormat.PaymentTypes.Credit,
    };

    public static SalesKpiDto Build(IReadOnlyList<SalesReportRow> salesInPeriod)
    {
        int salesCount = salesInPeriod.Count;
        decimal totalRevenue = salesInPeriod.Sum(s => s.TotalAmount);

        // Unknown-profit sales (free-form, no cost) are excluded from the profit total rather than
        // counted as zero — same rule as GetSummaryHandler/DashboardCalculator.
        decimal totalProfit = salesInPeriod.Sum(s => s.Profit ?? 0m);
        int unknownProfitSalesCount = salesInPeriod.Count(s => s.Profit is null);
        decimal unknownProfitAmount = salesInPeriod.Where(s => s.Profit is null).Sum(s => s.TotalAmount);

        decimal avgSale = salesCount == 0 ? 0m : totalRevenue / salesCount;

        List<PaymentTypeKpiDto> byPayment = PaymentTypeOrder
            .Select(type =>
            {
                IReadOnlyList<SalesReportRow> rows = salesInPeriod.Where(s => s.PaymentType == type).ToList();
                return new PaymentTypeKpiDto(type, rows.Sum(s => s.TotalAmount), rows.Sum(s => s.Profit ?? 0m));
            })
            .ToList();

        return new SalesKpiDto(
            salesCount,
            totalRevenue,
            totalProfit,
            unknownProfitSalesCount,
            unknownProfitAmount,
            byPayment,
            avgSale);
    }
}
