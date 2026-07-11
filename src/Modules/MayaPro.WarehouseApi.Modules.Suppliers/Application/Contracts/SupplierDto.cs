namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;

/// <summary>
/// A supplier as returned by the API. <see cref="PaidAmount"/> (total payments we made) and
/// <see cref="LastPaymentDate"/> are computed server-side.
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
    DateTime CreatedAt,
    DateTime UpdatedAt);
