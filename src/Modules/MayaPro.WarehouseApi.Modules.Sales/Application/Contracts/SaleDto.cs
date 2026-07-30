namespace MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;

/// <summary>
/// A sale as returned by the API. Field names follow the frontend <c>Sale</c> type (<c>salePrice</c>,
/// <c>costPerUnit</c>, <c>employeeId</c>, <c>createdAt</c>), with <c>soldByName</c> added for display.
/// <para>
/// For a free-form (manual) sale, <c>productId</c> is null, <c>isManual</c> is true, and <c>costPerUnit</c>
/// / <c>profit</c> are null when the seller did not supply a cost — the revenue is recorded but the gain is
/// unknown. <c>category</c> is the snapshot at sale time (product category for catalogued sales; optional
/// for manual; null on older rows).
/// </para>
/// <para>
/// BE#15 — qismən ödənişli satış: <c>paidAmount</c> is how much was actually received (equals
/// <c>totalAmount</c> on a fully paid sale); <c>remainingAmount</c> is the computed
/// <c>totalAmount − paidAmount</c> (zero when fully paid); <c>paidVia</c> (<c>"Nağd"|"Kart"</c>) is how the
/// paid portion was received — only meaningful (may differ from <c>paymentType</c>) when
/// <c>remainingAmount</c> is positive, i.e. a Nisyə sale with a cash/card down-payment.
/// </para>
/// </summary>
public sealed record SaleDto(
    Guid Id,
    Guid? ProductId,
    string ProductName,
    string? Category,
    int Quantity,
    decimal SalePrice,
    decimal Subtotal,
    decimal TotalAmount,
    decimal? CostPerUnit,
    decimal? PurchasePricePerUnit,
    decimal? Profit,
    string PaymentType,
    Guid? CustomerId,
    Guid? EmployeeId,
    string SoldByName,
    DateTime CreatedAt,
    bool IsManual,
    IReadOnlyList<SaleExpenseItemDto> ExpenseItems,
    decimal PaidAmount,
    decimal RemainingAmount,
    string PaidVia);
