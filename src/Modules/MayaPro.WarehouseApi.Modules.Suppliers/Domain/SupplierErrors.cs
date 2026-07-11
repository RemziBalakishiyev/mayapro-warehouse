using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Domain;

/// <summary>Business errors for the Suppliers module. Messages are user-facing (Azerbaijani).</summary>
public static class SupplierErrors
{
    public static readonly Error NotFound =
        new("Suppliers.NotFound", "Təchizatçı tapılmadı");

    public static readonly Error PaymentExceedsDebt =
        new("Suppliers.PaymentExceedsDebt", "Ödəniş borcdan çox ola bilməz");
}
