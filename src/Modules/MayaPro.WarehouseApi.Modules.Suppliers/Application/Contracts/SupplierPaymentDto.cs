namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;

/// <summary>A supplier payment as returned by the API.</summary>
public sealed record SupplierPaymentDto(
    Guid Id,
    Guid SupplierId,
    decimal Amount,
    string? Note,
    Guid? PaidByUserId,
    DateTime Date);
