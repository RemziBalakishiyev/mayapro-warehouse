namespace MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;

/// <summary>
/// A customer as returned by the API. <see cref="PaidAmount"/> (total payments received),
/// <see cref="LastPurchaseDate"/> (last credit sale) and <see cref="LastPaymentDate"/> are computed
/// server-side so the frontend need not aggregate.
/// </summary>
public sealed record CustomerDto(
    Guid Id,
    string Name,
    string? Phone,
    string? Note,
    decimal Debt,
    decimal PaidAmount,
    DateTime? LastPurchaseDate,
    DateTime? LastPaymentDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);
