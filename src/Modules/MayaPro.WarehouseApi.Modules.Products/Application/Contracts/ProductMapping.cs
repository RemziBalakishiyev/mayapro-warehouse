using MayaPro.WarehouseApi.Modules.Products.Domain;

namespace MayaPro.WarehouseApi.Modules.Products.Application.Contracts;

/// <summary>Maps the <see cref="Product"/> entity to its wire DTO.</summary>
public static class ProductMapping
{
    public static ProductDto ToDto(this Product product) =>
        new(
            product.Id,
            product.Name,
            product.Category,
            product.Attributes.Select(a => new ProductAttributeDto(a.Name, a.Value)).ToList(),
            product.Barcode,
            product.Image,
            product.Note,
            product.PurchasePrice,
            product.SalePrice,
            product.Quantity,
            product.InitialQuantity,
            product.MinStock,
            product.Currency,
            product.SupplierId,
            product.Location,
            product.Store,
            product.Warehouse,
            product.Shelf,
            product.Box,
            product.Expenses.Select(e => new ProductExpenseItemDto(e.Name, e.Amount)).ToList(),
            product.RealCostPerUnit,
            product.CreatedAt,
            product.UpdatedAt);
}
