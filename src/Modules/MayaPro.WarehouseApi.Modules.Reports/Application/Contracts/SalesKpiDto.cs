namespace MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;

/// <summary>
/// BE#27 — <c>GET /api/reports/sales-kpi?from=&amp;to=</c>. Mirrors <see cref="SummaryDto"/>'s
/// unknown-profit rule: sales whose cost is unknown (free-form, no cost) are excluded from
/// <see cref="TotalProfit"/> rather than counted as zero, and reported separately via
/// <see cref="UnknownProfitSalesCount"/>/<see cref="UnknownProfitAmount"/> — their revenue still counts
/// toward <see cref="TotalRevenue"/> and their payment type's row in <see cref="ByPayment"/>.
/// </summary>
public sealed record SalesKpiDto(
    int SalesCount,
    decimal TotalRevenue,
    decimal TotalProfit,
    int UnknownProfitSalesCount,
    decimal UnknownProfitAmount,
    IReadOnlyList<PaymentTypeKpiDto> ByPayment,
    decimal AvgSale);

/// <summary>
/// One payment method's slice of the period: revenue and profit summed only over that method's rows.
/// <see cref="Type"/> is the wire code (<see cref="MayaPro.WarehouseApi.SharedKernel.Contracts.WireFormat.PaymentTypes"/>).
/// Always present for Nağd/Kart/Nisyə, even at zero, so the frontend never has to guess a missing row.
/// </summary>
public sealed record PaymentTypeKpiDto(string Type, decimal Revenue, decimal Profit);
