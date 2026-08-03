namespace MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;

/// <summary>
/// A trading summary over a period (today / week / month / all). Revenue and profit come from the sales,
/// expenses from the expenses, and <see cref="NetProfit"/> is profit net of those expenses. For "all"
/// the range is unbounded, so <see cref="From"/> / <see cref="To"/> are null.
/// <para>
/// Free-form sales whose cost is unknown carry no profit: their revenue is included in <see cref="SalesTotal"/>
/// and the payment splits, but they are excluded from <see cref="Profit"/> (not counted as zero) and reported
/// separately via <see cref="UnknownProfitSalesCount"/> and <see cref="UnknownProfitAmount"/> (their revenue sum).
/// </para>
/// <para>
/// <see cref="GeneralExpenses"/> + <see cref="ProductExpenses"/> + <see cref="SalaryExpenses"/> always sums
/// to <see cref="Expenses"/> — <c>GeneralExpenses</c>/<c>ProductExpenses</c> are a split of the Expenses
/// module's total by <c>Expense.Source</c> ("general" = no product effect, "product" = raised a product's
/// real cost), and <c>SalaryExpenses</c> is a separate source (employee salary <b>payments</b>) folded into
/// the same total. <see cref="NetProfit"/> keeps using the single total, so the split never changes it.
/// </para>
/// <para>
/// BE#33: <see cref="SalaryExpenses"/> is the period's salary <b>payments</b> only (<c>ISalaryModule</c>
/// never returns deductions — see its docs) — the same figure <c>CloseDayHandler</c> folds into a closing's
/// <c>Expenses</c>, so this preview and the actual day-end close never disagree on "today"'s salary payments.
/// </para>
/// </summary>
public sealed record SummaryDto(
    string Period,
    DateOnly? From,
    DateOnly? To,
    decimal SalesTotal,
    decimal Profit,
    decimal Expenses,
    int SalesCount,
    decimal NetProfit,
    decimal CashSales,
    decimal CardSales,
    decimal CreditSales,
    int UnknownProfitSalesCount,
    decimal UnknownProfitAmount,
    decimal GeneralExpenses,
    decimal ProductExpenses,
    decimal SalaryExpenses);
