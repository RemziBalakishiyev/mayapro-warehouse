using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CreateProduct;

/// <summary>
/// Input for creating a product. Mirrors the frontend <c>NewProduct</c> (Product minus the computed
/// fields id/realCostPerUnit/initialQuantity/createdAt/updatedAt). <c>InitialQuantity</c> is fixed to
/// <c>Quantity</c> at creation.
/// </summary>
public sealed record CreateProductCommand(
    string Name,
    string Category,
    IReadOnlyList<ProductAttributeDto> Attributes,
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
