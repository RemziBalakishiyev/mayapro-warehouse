using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Customers.Domain;

/// <summary>Business errors for the Customers module. Messages are user-facing (Azerbaijani).</summary>
public static class CustomerErrors
{
    public static readonly Error NotFound =
        new("Customers.NotFound", "Müştəri tapılmadı");

    public static readonly Error PaymentExceedsDebt =
        new("Customers.PaymentExceedsDebt", "Ödəniş borcdan çox ola bilməz");
}
