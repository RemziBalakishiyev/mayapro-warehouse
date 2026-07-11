using System.Text.Json.Serialization;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

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

/// <summary>
/// The batch-expense breakdown. The C# members are English, but the JSON keys stay the frontend
/// <c>ExpenseBreakdown</c> keys (<c>yol/fehle/yer/paket/diger</c>) via <see cref="JsonPropertyNameAttribute"/> —
/// they are the wire contract and must not change.
/// </summary>
public sealed record ExpenseBreakdownDto(
    [property: JsonPropertyName(WireFormat.ExpenseKeys.Transport)] decimal Transport,
    [property: JsonPropertyName(WireFormat.ExpenseKeys.Labor)] decimal Labor,
    [property: JsonPropertyName(WireFormat.ExpenseKeys.Storage)] decimal Storage,
    [property: JsonPropertyName(WireFormat.ExpenseKeys.Packaging)] decimal Packaging,
    [property: JsonPropertyName(WireFormat.ExpenseKeys.Other)] decimal Other);
