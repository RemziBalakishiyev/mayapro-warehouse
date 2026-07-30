namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>
/// BE#19: the "real money received" Cash/Card/Credit split of a set of <see cref="SalesReportRow"/>s — the
/// same rule <see cref="ISalesModule.GetDayTotalsAsync"/> applies at the database level, expressed once here
/// for the in-memory callers (the Reports summary and the sales PDF export) instead of each re-deriving it
/// and drifting, which is exactly the bug BE#19 fixed: both had kept the pre-BE#15 "TotalAmount of the sales
/// whose PaymentType is X" formula and so disagreed with the dashboard/day-end figures on any partially paid
/// sale.
/// </summary>
public static class SalesReportRowTotals
{
    /// <summary>
    /// Cash/Card are the amounts actually received by each method — bucketed by
    /// <see cref="SalesReportRow.ReceivedVia"/>, summing <see cref="SalesReportRow.ReceivedAmount"/>, so a Nisyə
    /// sale's cash/card down-payment counts as the real income it is. Credit is only what a Nisyə row still
    /// owes (<see cref="SalesReportRow.TotalAmount"/> − <see cref="SalesReportRow.ReceivedAmount"/>), never its
    /// full total.
    /// <para>
    /// Nothing is clamped or rounded on purpose: a sale can never be stored with a negative or above-total
    /// <c>PaidAmount</c> (<c>SaleWriteValidator</c>'s rule), and the day-end SQL sums the same raw decimals —
    /// clamping only here would silently re-introduce the very drift this helper exists to prevent.
    /// Consequently Cash + Card + Credit equals the period's total sales for any stored set of rows.
    /// </para>
    /// <para>
    /// The window is the caller's (a day for day-end, a whole period for the reports); the
    /// <see cref="SalesDayTotals"/> shape is shared with the day-end contract so the two paths stay directly
    /// comparable.
    /// </para>
    /// </summary>
    public static SalesDayTotals ComputeReceivedTotals(this IEnumerable<SalesReportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        decimal cash = 0m;
        decimal card = 0m;
        decimal credit = 0m;

        // One pass: the three figures then always come from the same snapshot of the same rows.
        foreach (SalesReportRow row in rows)
        {
            // Money can only physically arrive as cash or card ("Nisyə" is an owing, not a method — the Sales
            // domain never stores it as a PaidVia), so a row lands in at most one of the two buckets.
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
