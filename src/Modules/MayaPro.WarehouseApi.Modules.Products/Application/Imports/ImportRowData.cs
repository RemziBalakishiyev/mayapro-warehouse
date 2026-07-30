using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;

namespace MayaPro.WarehouseApi.Modules.Products.Application.Imports;

/// <summary>
/// A successfully-parsed import row's fields — on the wire as <c>rows[i].data</c> for a
/// <c>create</c>/<c>update</c> row, and reused unchanged as the values <c>commit</c> applies. <c>null</c>
/// for an <c>error</c> row.
/// </summary>
public sealed record ImportRowData(
    string Name,
    string Category,
    string Barcode,
    decimal PurchasePrice,
    decimal SalePrice,
    int Quantity,
    int MinStock,
    string Warehouse,
    string Store,
    string Shelf,
    string Box,
    IReadOnlyList<ProductAttributeDto> Attributes,
    string Note);
