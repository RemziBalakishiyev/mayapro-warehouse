using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Domain;

/// <summary>Business errors for the DayEnd module. Messages are user-facing (Azerbaijani).</summary>
public static class DayEndErrors
{
    public static readonly Error AlreadyClosed =
        new("DayEnd.AlreadyClosed", "Bu gün artıq bağlanıb");
}
