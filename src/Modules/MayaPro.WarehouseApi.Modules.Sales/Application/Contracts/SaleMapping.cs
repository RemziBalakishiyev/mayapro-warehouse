using MayaPro.WarehouseApi.Modules.Sales.Domain;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;

/// <summary>Maps the <see cref="Sale"/> entity to its wire DTO.</summary>
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
            sale.IsManual);
}
