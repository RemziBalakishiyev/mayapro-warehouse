using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDebtsKpi;

/// <summary>
/// BE#27 — fetches everything the debts-kpi endpoint needs from the Customers and Sales read contracts and
/// hands it to the pure <see cref="DebtsKpiCalculator"/>. An empty <c>from</c>/<c>to</c> means the whole
/// history is used for the period-scoped fields (unbounded); a reversed range (<c>from &gt; to</c>) is
/// rejected rather than coerced.
/// </summary>
public sealed class GetDebtsKpiHandler(ICustomersModule customers, ISalesModule sales, IDateProvider dateProvider)
{
    public async Task<Result<DebtsKpiDto>> Handle(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (from is { } f && to is { } t && f > t)
            return Result.Failure<DebtsKpiDto>(ReportErrors.InvalidDateRange);

        // "As of now" fields — unbounded regardless of from/to.
        decimal totalOutstanding = await customers.GetTotalDebtAsync(ct);
        IReadOnlyList<CustomerDebtRow> debtors = await customers.GetDebtorsAsync(ct);

        // Period-scoped fields.
        IReadOnlyList<SalesReportRow> salesInPeriod = await sales.GetSalesAsync(from, to, ct);
        IReadOnlyList<CustomerPaymentRow> paymentsInPeriod = await customers.GetPaymentsAsync(from, to, ct);

        // Oldest still-open debt source, oldest-first per ISalesModule.GetOutstandingSalesAsync — resolved
        // to a business-zone date here so the (pure) calculator only ever does DateOnly arithmetic.
        IReadOnlyList<CustomerOutstandingSale> outstanding = await sales.GetOutstandingSalesAsync(ct);
        DateOnly? oldestDebtDate = outstanding.Count == 0 ? null : dateProvider.ToLocalDate(outstanding[0].Date);

        return Result.Success(DebtsKpiCalculator.Build(
            totalOutstanding, debtors, salesInPeriod, paymentsInPeriod, oldestDebtDate, dateProvider.Today));
    }
}
