using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSummary;

/// <summary>
/// Builds a trading summary over a period from the Sales, Expenses and Salary read contracts. Net profit is
/// the sales profit less the period's expenses (store expenses plus salary payments). An unrecognised period
/// is rejected (400) rather than coerced.
/// </summary>
public sealed class GetSummaryHandler(
    ISalesModule sales, IExpensesModule expenses, ISalaryModule salary, IDateProvider dateProvider)
{
    public async Task<Result<SummaryDto>> Handle(string? period, CancellationToken ct)
    {
        if (!ReportPeriod.TryResolve(period, dateProvider.Today, out ReportPeriod window))
            return Result.Failure<SummaryDto>(ReportErrors.InvalidPeriod);

        IReadOnlyList<SalesReportRow> salesRows = await sales.GetSalesAsync(window.From, window.To, ct);
        IReadOnlyList<ExpenseReportRow> expenseRows = await expenses.GetExpensesAsync(window.From, window.To, ct);
        // BE#33: salary PAYMENTS made in the period are cash out of the same drawer as a store expense, so
        // they belong in the "expected cash before closing" preview exactly like CloseDayHandler already
        // folds them into the actual close — ISalaryModule never returns deductions (see its docs), so no
        // extra filtering is needed here.
        IReadOnlyList<SalaryPaymentRow> salaryRows = await salary.GetPaymentsAsync(window.From, window.To, ct);

        decimal salesTotal = salesRows.Sum(s => s.TotalAmount);
        // Unknown-profit sales (free-form, no cost) are excluded from the profit total rather than counted
        // as zero, and reported separately so the frontend can flag "N sales with unknown profit".
        decimal profit = salesRows.Sum(s => s.Profit ?? 0m);
        // Same total, split by source — general expenses never touched a product's cost. Only the
        // product side is filtered; general is the remainder, so the two parts always add up to the
        // store-expense total exactly, whatever the source vocabulary grows into (anything that did not
        // raise a product's cost is a general store expense by definition).
        decimal productExpenses = expenseRows
            .Where(e => e.Source == WireFormat.ExpenseSources.Product).Sum(e => e.Amount);
        decimal generalExpenses = expenseRows.Sum(e => e.Amount) - productExpenses;
        decimal salaryExpenses = salaryRows.Sum(p => p.Amount);
        // BE#33: expenses = generalExpenses + productExpenses + salaryExpenses, so the split always sums to
        // the total shown, and NetProfit reflects the same salary payments CloseDayHandler will fold in.
        decimal expensesTotal = generalExpenses + productExpenses + salaryExpenses;

        // BE#19: Cash/Card/Credit are the "real money received" split — the same formula the day-end/dashboard
        // side applies (SalesModuleContract.GetDayTotalsAsync), factored into one shared helper so the two
        // never drift again. Credit is only what remains unpaid, not the row's full total.
        SalesDayTotals receivedTotals = salesRows.ComputeReceivedTotals();

        return Result.Success(new SummaryDto(
            Period: window.Code,
            From: window.From,
            To: window.To,
            SalesTotal: salesTotal,
            Profit: profit,
            Expenses: expensesTotal,
            SalesCount: salesRows.Count,
            NetProfit: profit - expensesTotal,
            CashSales: receivedTotals.Cash,
            CardSales: receivedTotals.Card,
            CreditSales: receivedTotals.Credit,
            UnknownProfitSalesCount: salesRows.Count(s => s.Profit is null),
            UnknownProfitAmount: salesRows.Where(s => s.Profit is null).Sum(s => s.TotalAmount),
            GeneralExpenses: generalExpenses,
            ProductExpenses: productExpenses,
            SalaryExpenses: salaryExpenses));
    }
}
