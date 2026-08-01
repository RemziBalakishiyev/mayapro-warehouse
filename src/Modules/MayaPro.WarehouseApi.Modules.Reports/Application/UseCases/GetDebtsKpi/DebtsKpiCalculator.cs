using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDebtsKpi;

/// <summary>
/// Pure debts-KPI maths (BE#27). Takes the raw data already fetched from the Customers and Sales read
/// contracts, plus the oldest open-debt source's business-zone date already resolved by the handler (see
/// <c>GetDebtsKpiHandler</c>), and produces the finished <see cref="DebtsKpiDto"/>. Side-effect free and
/// fully unit-testable without a database or an <c>IDateProvider</c>.
/// </summary>
public static class DebtsKpiCalculator
{
    public static DebtsKpiDto Build(
        decimal totalOutstanding,
        IReadOnlyList<CustomerDebtRow> debtors,
        IReadOnlyList<SalesReportRow> salesInPeriod,
        IReadOnlyList<CustomerPaymentRow> paymentsInPeriod,
        DateOnly? oldestDebtDate,
        DateOnly today)
    {
        // Tie-break behaviour (DK-U5): highest debt first; equal amounts fall back to the name
        // (alphabetical), so the result is deterministic rather than depending on query/enumeration order.
        TopDebtorDto? topDebtor = debtors.Count == 0
            ? null
            : debtors
                .OrderByDescending(d => d.Debt)
                .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => new TopDebtorDto(d.Name, d.Debt))
                .First();

        // Down-payment aware: a Nisyə sale's new debt is TotalAmount − ReceivedAmount (what actually
        // raised the customer's balance), never the sale's full total.
        decimal periodNewDebt = salesInPeriod
            .Where(s => s.PaymentType == WireFormat.PaymentTypes.Credit)
            .Sum(s => s.TotalAmount - s.ReceivedAmount);

        decimal periodCollected = paymentsInPeriod.Sum(p => p.Amount);

        int? oldestDebtDays = oldestDebtDate is { } date ? Math.Max(0, today.DayNumber - date.DayNumber) : null;

        return new DebtsKpiDto(
            totalOutstanding,
            debtors.Count,
            topDebtor,
            periodNewDebt,
            periodCollected,
            oldestDebtDays);
    }
}
