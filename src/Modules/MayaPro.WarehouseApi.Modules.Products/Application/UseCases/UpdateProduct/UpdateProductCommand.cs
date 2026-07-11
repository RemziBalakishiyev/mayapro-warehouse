using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.UpdateProduct;

/// <summary>
/// Input for editing a product. Same fields as create (frontend <c>ProductUpdate</c>); the target is
/// identified by <see cref="Id"/> (taken from the route). <c>InitialQuantity</c> is not editable.
/// </summary>
public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string Category,
    string Size,
    string Color,
    string Model,
    string Barcode,
    string Image,
    string Note,
    decimal PurchasePrice,
    decimal SalePrice,
    int Quantity,
    int MinStock,
    string Currency,
    string SupplierId,
    string Location,
    string Store,
    string Warehouse,
    string Shelf,
    string Box,
    ExpenseBreakdownDto Expenses);
