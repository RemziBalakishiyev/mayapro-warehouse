using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Sales.Domain;

/// <summary>Business errors for the Sales module. Messages are user-facing (Azerbaijani).</summary>
public static class SaleErrors
{
    public static readonly Error NotFound =
        new("Sales.NotFound", "Satış tapılmadı");
}
