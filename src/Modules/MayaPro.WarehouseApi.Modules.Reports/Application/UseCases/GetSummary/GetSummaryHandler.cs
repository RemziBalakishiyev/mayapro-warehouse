using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSummary;

/// <summary>
/// Builds a trading summary over a period from the Sales and Expenses read contracts. Net profit is the
/// sales profit less the period's expenses. An unrecognised period is rejected (400) rather than coerced.
/// </summary>
public sealed class GetSummaryHandler(ISalesModule sales, IExpensesModule expenses, IDateProvider dateProvider)
{
    public async Task<Result<SummaryDto>> Handle(string? period, CancellationToken ct)
    {
        if (!ReportPeriod.TryResolve(period, dateProvider.Today, out ReportPeriod window))
            return Result.Failure<SummaryDto>(ReportErrors.InvalidPeriod);

        IReadOnlyList<SalesReportRow> salesRows = await sales.GetSalesAsync(window.From, window.To, ct);
        IReadOnlyList<ExpenseReportRow> expenseRows = await expenses.GetExpensesAsync(window.From, window.To, ct);

        decimal salesTotal = salesRows.Sum(s => s.TotalAmount);
        // Unknown-profit sales (free-form, no cost) are excluded from the profit total rather than counted
        // as zero, and reported separately so the frontend can flag "N sales with unknown profit".
        decimal profit = salesRows.Sum(s => s.Profit ?? 0m);
        decimal expensesTotal = expenseRows.Sum(e => e.Amount);

        return Result.Success(new SummaryDto(
            Period: window.Code,
            From: window.From,
            To: window.To,
            SalesTotal: salesTotal,
            Profit: profit,
            Expenses: expensesTotal,
            SalesCount: salesRows.Count,
            NetProfit: profit - expensesTotal,
            CashSales: salesRows.Where(s => s.PaymentType == WireFormat.PaymentTypes.Cash).Sum(s => s.TotalAmount),
            CardSales: salesRows.Where(s => s.PaymentType == WireFormat.PaymentTypes.Card).Sum(s => s.TotalAmount),
            CreditSales: salesRows.Where(s => s.PaymentType == WireFormat.PaymentTypes.Credit).Sum(s => s.TotalAmount),
            UnknownProfitSalesCount: salesRows.Count(s => s.Profit is null),
            UnknownProfitAmount: salesRows.Where(s => s.Profit is null).Sum(s => s.TotalAmount)));
    }
}
