namespace MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;

/// <summary>
/// A sale as returned by the API. Field names follow the frontend <c>Sale</c> type (<c>salePrice</c>,
/// <c>costPerUnit</c>, <c>employeeId</c>, <c>createdAt</c>), with <c>soldByName</c> added for display.
/// <para>
/// For a free-form (manual) sale, <c>productId</c> is null, <c>isManual</c> is true, and <c>costPerUnit</c>
/// / <c>profit</c> are null when the seller did not supply a cost — the revenue is recorded but the gain is
/// unknown.
/// </para>
/// </summary>
public sealed record SaleDto(
    Guid Id,
    Guid? ProductId,
    string ProductName,
    int Quantity,
    decimal SalePrice,
    decimal Subtotal,
    decimal Discount,
    decimal TotalAmount,
    decimal? CostPerUnit,
    decimal? Profit,
    string PaymentType,
    Guid? CustomerId,
    Guid? EmployeeId,
    string SoldByName,
    DateTime CreatedAt,
    bool IsManual);
