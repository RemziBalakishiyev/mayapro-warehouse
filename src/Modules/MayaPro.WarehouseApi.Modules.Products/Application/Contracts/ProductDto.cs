namespace MayaPro.WarehouseApi.Modules.Products.Application.Contracts;

/// <summary>
/// A product as returned by the API. Field-for-field the frontend <c>Product</c> type (camelCase on the
/// wire), so the frontend can drop the mock and read this directly.
/// </summary>
public sealed record ProductDto(
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
    int InitialQuantity,
    int MinStock,
    string Currency,
    string SupplierId,
    string Location,
    string Store,
    string Warehouse,
    string Shelf,
    string Box,
    ExpenseBreakdownDto Expenses,
    decimal RealCostPerUnit,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>The batch-expense breakdown; mirrors the frontend <c>ExpenseBreakdown</c> keys.</summary>
public sealed record ExpenseBreakdownDto(
    decimal Yol,
    decimal Fehle,
    decimal Yer,
    decimal Paket,
    decimal Diger);
