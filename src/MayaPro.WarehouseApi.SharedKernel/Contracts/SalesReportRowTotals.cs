namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>
/// BE#19: the "real money received" Cash/Card/Credit split, computed from <see cref="SalesReportRow"/>s —
/// the exact same rule <see cref="ISalesModule.GetDayTotalsAsync"/> applies at the database level
/// (<c>SalesModuleContract</c>), factored out here so the read-only Reports summary and the sales PDF
/// export use this one formula instead of each re-deriving it (and drifting, as BE#19 found — both had
/// kept the pre-BE#15 "TotalAmount of sales whose PaymentType is X" formula).
/// <para>
/// Cash/Card bucket by <see cref="SalesReportRow.ReceivedVia"/> and sum <see cref="SalesReportRow.ReceivedAmount"/>
/// — which also picks up a Nisyə sale's cash/card down-payment. Credit is only what remains unpaid per row
/// (<see cref="SalesReportRow.TotalAmount"/> minus <see cref="SalesReportRow.ReceivedAmount"/>), never the
/// row's full total, so a partially paid Nisyə sale contributes its down-payment to Cash/Card and only the
/// remainder to Credit.
/// </para>
/// </summary>
public static class SalesReportRowTotals
{
    public static SalesDayTotals ComputeReceivedTotals(this IEnumerable<SalesReportRow> rows)
    {
        decimal cash = 0m;
        decimal card = 0m;
        decimal credit = 0m;

        foreach (SalesReportRow row in rows)
        {
            if (row.ReceivedVia == WireFormat.PaymentTypes.Cash)
                cash += row.ReceivedAmount;
            else if (row.ReceivedVia == WireFormat.PaymentTypes.Card)
                card += row.ReceivedAmount;

            if (row.PaymentType == WireFormat.PaymentTypes.Credit)
                credit += row.TotalAmount - row.ReceivedAmount;
        }

        return new SalesDayTotals(cash, card, credit);
    }
}
