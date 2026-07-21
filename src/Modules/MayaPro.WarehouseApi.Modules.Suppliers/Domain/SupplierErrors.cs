using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Domain;

/// <summary>Business errors for the Suppliers module. Messages are user-facing (Azerbaijani).</summary>
public static class SupplierErrors
{
    public static readonly Error NotFound =
        new("Suppliers.NotFound", "Təchizatçı tapılmadı");

    public static readonly Error PaymentExceedsDebt =
        new("Suppliers.PaymentExceedsDebt", "Ödəniş borcdan çox ola bilməz");

    /// <summary>
    /// A supplier we still owe cannot be deleted. Code ends in <c>Conflict</c> so the shared Result→HTTP
    /// convention maps it to 409.
    /// </summary>
    public static readonly Error HasDebtConflict =
        new("Suppliers.HasDebtConflict", "Borcumuz olan təchizatçı silinə bilməz");
}
