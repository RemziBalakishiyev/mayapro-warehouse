using MayaPro.WarehouseApi.Modules.DayEnd.Domain;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Application.Contracts;

/// <summary>Maps the <see cref="Closing"/> entity to its wire DTO (NisyeSales → creditSales).</summary>
public static class ClosingMapping
{
    public static ClosingDto ToDto(this Closing closing) =>
        new(
            closing.Id,
            closing.Date,
            closing.OpeningCash,
            closing.CashSales,
            closing.CardSales,
            closing.NisyeSales,
            closing.Expenses,
            closing.ExpectedCash,
            closing.ActualCash,
            closing.Difference,
            closing.ClosedByUserId,
            closing.Note,
            closing.CreatedAt);
}
