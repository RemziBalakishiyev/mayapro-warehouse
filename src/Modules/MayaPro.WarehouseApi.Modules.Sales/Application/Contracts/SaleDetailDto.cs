namespace MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;

/// <summary>
/// The full detail of a single sale (<c>GET /api/sales/{id}</c>). Carries every <see cref="SaleDto"/> field
/// plus two cross-module extras:
/// <list type="bullet">
///   <item><see cref="CustomerName"/> — the customer's name for a credit (Nisyə) sale; null otherwise.</item>
///   <item><see cref="CurrentProductName"/> — the product's <em>current</em> catalogue name for a catalogued
///     sale (null for manual sales or a since-deleted product). <see cref="ProductName"/> remains the
///     sale-time snapshot, so the caller can show both "sold as" and "now called".</item>
/// </list>
/// </summary>
public sealed record SaleDetailDto(
    Guid Id,
    Guid? ProductId,
    string ProductName,
    string? CurrentProductName,
    string? Category,
    int Quantity,
    decimal SalePrice,
    decimal Subtotal,
    decimal Discount,
    decimal TotalAmount,
    decimal? CostPerUnit,
    decimal? Profit,
    string PaymentType,
    Guid? CustomerId,
    string? CustomerName,
    Guid? EmployeeId,
    string SoldByName,
    DateTime CreatedAt,
    bool IsManual,
    IReadOnlyList<SaleExpenseItemDto> ExpenseItems);
