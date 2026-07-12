using MayaPro.WarehouseApi.Modules.Suppliers.Domain;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;

/// <summary>Maps Suppliers entities to their wire DTOs.</summary>
public static class SupplierMapping
{
    public static SupplierDto ToDto(
        this Supplier supplier,
        decimal paidAmount = 0m,
        DateTime? lastPaymentDate = null,
        int itemCount = 0) =>
        new(supplier.Id, supplier.Name, supplier.ContactName, supplier.Phone, supplier.Note,
            supplier.Debt, paidAmount, lastPaymentDate, itemCount, supplier.CreatedAt, supplier.UpdatedAt);

    public static SupplierPaymentDto ToDto(this SupplierPayment payment) =>
        new(payment.Id, payment.SupplierId, payment.Amount, payment.Note,
            payment.PaidByUserId, payment.Date);
}
