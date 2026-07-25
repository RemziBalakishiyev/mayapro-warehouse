namespace MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;

/// <summary>
/// A customer as returned by the API. <see cref="PaidAmount"/> (total payments received),
/// <see cref="TotalPurchases"/>/<see cref="PurchaseCount"/> (lifetime sales of EVERY payment type),
/// <see cref="LastPurchaseDate"/> (last sale of any type) and <see cref="LastPaymentDate"/> are computed
/// server-side so the frontend need not aggregate. <see cref="InitialDebt"/> is the opening balance the
/// customer was migrated in with (0 when they started clean) — it is the first row of their debt history.
/// </summary>
public sealed record CustomerDto(
    Guid Id,
    string Name,
    string? Phone,
    string? Note,
    decimal Debt,
    decimal InitialDebt,
    decimal PaidAmount,
    decimal TotalPurchases,
    int PurchaseCount,
    DateTime? LastPurchaseDate,
    DateTime? LastPaymentDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);
