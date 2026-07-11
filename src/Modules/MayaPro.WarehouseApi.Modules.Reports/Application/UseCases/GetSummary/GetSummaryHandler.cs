using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSummary;

/// <summary>
/// Builds a trading summary over a period from the Sales and Expenses read contracts. Net profit is the
/// sales profit less the period's expenses. An unrecognised period is rejected (400) rather than coerced.
/// </summary>
public sealed class GetSummaryHandler(ISalesModule sales, IExpensesModule expenses)
{
    // The wire payment codes (frontend contract) — Reports references SharedKernel only, not the Sales domain.
    private const string CashCode = "Nağd";
    private const string CardCode = "Kart";
    private const string CreditCode = "Nisyə";

    public async Task<Result<SummaryDto>> Handle(string? period, DateOnly today, CancellationToken ct)
    {
        if (!ReportPeriod.TryResolve(period, today, out ReportPeriod window))
            return Result.Failure<SummaryDto>(ReportErrors.InvalidPeriod);

        IReadOnlyList<SalesReportRow> salesRows = await sales.GetSalesAsync(window.From, window.To, ct);
        IReadOnlyList<ExpenseReportRow> expenseRows = await expenses.GetExpensesAsync(window.From, window.To, ct);

        decimal salesTotal = salesRows.Sum(s => s.TotalAmount);
        decimal profit = salesRows.Sum(s => s.Profit);
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
            CashSales: salesRows.Where(s => s.PaymentType == CashCode).Sum(s => s.TotalAmount),
            CardSales: salesRows.Where(s => s.PaymentType == CardCode).Sum(s => s.TotalAmount),
            CreditSales: salesRows.Where(s => s.PaymentType == CreditCode).Sum(s => s.TotalAmount)));
    }
}
