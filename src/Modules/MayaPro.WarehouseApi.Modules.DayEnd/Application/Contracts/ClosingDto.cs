namespace MayaPro.WarehouseApi.Modules.DayEnd.Application.Contracts;

/// <summary>
/// A day-end closing as returned by the API. Field names follow the frontend <c>Closing</c> type
/// (<c>creditSales</c> for the Nisyə total).
/// </summary>
public sealed record ClosingDto(
    Guid Id,
    DateOnly Date,
    decimal OpeningCash,
    decimal CashSales,
    decimal CardSales,
    decimal CreditSales,
    decimal Expenses,
    decimal ExpectedCash,
    decimal ActualCash,
    decimal Difference,
    Guid? ClosedByUserId,
    string? Note,
    DateTime CreatedAt);
