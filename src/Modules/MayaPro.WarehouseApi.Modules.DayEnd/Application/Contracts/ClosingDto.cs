namespace MayaPro.WarehouseApi.Modules.DayEnd.Application.Contracts;

/// <summary>
/// A day-end closing as returned by the API. Field names follow the frontend <c>Closing</c> type
/// (<c>creditSales</c> for the Nisyə total).
/// <para>
/// BE#33: <see cref="SalaryExpenses"/> is the part of <see cref="Expenses"/> that is employee salary
/// payments — additive breakdown only, <see cref="Expenses"/>/<see cref="ExpectedCash"/> are unchanged.
/// </para>
/// </summary>
public sealed record ClosingDto(
    Guid Id,
    DateOnly Date,
    decimal OpeningCash,
    decimal CashSales,
    decimal CardSales,
    decimal CreditSales,
    decimal Expenses,
    decimal SalaryExpenses,
    decimal ExpectedCash,
    decimal ActualCash,
    decimal Difference,
    Guid? ClosedByUserId,
    string? Note,
    DateTime CreatedAt);
