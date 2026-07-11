namespace MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;

/// <summary>
/// A trading summary over a period (today / week / month / all). Revenue and profit come from the sales,
/// expenses from the expenses, and <see cref="NetProfit"/> is profit net of those expenses. For "all"
/// the range is unbounded, so <see cref="From"/> / <see cref="To"/> are null.
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
    decimal CreditSales);
