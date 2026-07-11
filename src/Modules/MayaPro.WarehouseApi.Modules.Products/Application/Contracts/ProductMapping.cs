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
            product.Size,
            product.Color,
            product.Model,
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
            new ExpenseBreakdownDto(
                product.Expenses.Yol,
                product.Expenses.Fehle,
                product.Expenses.Yer,
                product.Expenses.Paket,
                product.Expenses.Diger),
            product.RealCostPerUnit,
            product.CreatedAt,
            product.UpdatedAt);
}
