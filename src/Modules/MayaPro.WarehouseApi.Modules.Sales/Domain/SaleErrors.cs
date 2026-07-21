using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Sales.Domain;

/// <summary>Business errors for the Sales module. Messages are user-facing (Azerbaijani).</summary>
public static class SaleErrors
{
    public static readonly Error NotFound =
        new("Sales.NotFound", "Satış tapılmadı");

    /// <summary>
    /// The sale's day has been closed, so it can no longer be edited or deleted. Code ends in
    /// <c>Conflict</c> so the shared Result→HTTP convention maps it to 409.
    /// </summary>
    public static readonly Error DayClosedConflict =
        new("Sales.DayClosedConflict", "Bu günün hesabı bağlanıb — satışa dəyişiklik etmək olmaz");
}
