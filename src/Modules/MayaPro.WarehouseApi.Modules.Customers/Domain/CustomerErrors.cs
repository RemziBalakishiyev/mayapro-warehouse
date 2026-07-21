using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Customers.Domain;

/// <summary>Business errors for the Customers module. Messages are user-facing (Azerbaijani).</summary>
public static class CustomerErrors
{
    public static readonly Error NotFound =
        new("Customers.NotFound", "Müştəri tapılmadı");

    public static readonly Error PaymentExceedsDebt =
        new("Customers.PaymentExceedsDebt", "Ödəniş borcdan çox ola bilməz");

    /// <summary>
    /// A customer with outstanding debt cannot be deleted. Code ends in <c>Conflict</c> so the shared
    /// Result→HTTP convention maps it to 409.
    /// </summary>
    public static readonly Error HasDebtConflict =
        new("Customers.HasDebtConflict", "Borcu olan müştəri silinə bilməz");
}
