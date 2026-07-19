using MayaPro.WarehouseApi.Modules.Sales.Domain;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;

/// <summary>Maps the <see cref="Sale"/> entity to its wire DTOs.</summary>
public static class SaleMapping
{
    public static SaleDto ToDto(this Sale sale) =>
        new(
            sale.Id,
            sale.ProductId,
            sale.ProductName,
            sale.Category,
            sale.Quantity,
            sale.UnitPrice,
            sale.Subtotal,
            sale.Discount,
            sale.TotalAmount,
            sale.CostPerUnit,
            sale.Profit,
            sale.PaymentType.ToCode(),
            sale.CustomerId,
            sale.SoldByUserId,
            sale.SoldByName,
            sale.Date,
            sale.IsManual,
            sale.ToExpenseItemDtos());

    /// <summary>
    /// Full single-sale detail: every <see cref="SaleDto"/> field plus the customer's name (resolved for
    /// credit sales) and the product's <em>current</em> catalogue name alongside the sale-time snapshot in
    /// <see cref="SaleDetailDto.ProductName"/>. Both extras are null when not applicable.
    /// </summary>
    public static SaleDetailDto ToDetailDto(this Sale sale, string? customerName, string? currentProductName) =>
        new(
            sale.Id,
            sale.ProductId,
            sale.ProductName,
            currentProductName,
            sale.Category,
            sale.Quantity,
            sale.UnitPrice,
            sale.Subtotal,
            sale.Discount,
            sale.TotalAmount,
            sale.CostPerUnit,
            sale.Profit,
            sale.PaymentType.ToCode(),
            sale.CustomerId,
            customerName,
            sale.SoldByUserId,
            sale.SoldByName,
            sale.Date,
            sale.IsManual,
            sale.ToExpenseItemDtos());

    private static IReadOnlyList<SaleExpenseItemDto> ToExpenseItemDtos(this Sale sale) =>
        sale.ExpenseItems.Select(e => new SaleExpenseItemDto(e.Name, e.Amount)).ToList();
}
