namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;

/// <summary>
/// A supplier as returned by the API. <see cref="PaidAmount"/> (total payments we made),
/// <see cref="LastPaymentDate"/> and <see cref="ItemCount"/> (products linked to this supplier) are
/// computed server-side.
/// </summary>
public sealed record SupplierDto(
    Guid Id,
    string Name,
    string? ContactName,
    string? Phone,
    string? Note,
    decimal Debt,
    decimal PaidAmount,
    DateTime? LastPaymentDate,
    int ItemCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);
