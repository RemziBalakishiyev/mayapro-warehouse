namespace MayaPro.WarehouseApi.Modules.Products.Application.Imports;

/// <summary>
/// The three ways a parsed Excel import row is classified. Plain strings on the wire (matching the
/// <c>ProductStatus</c> convention in Exports) rather than an enum, so the frontend reads them directly
/// without a numeric-to-label mapping.
/// </summary>
public static class ImportRowStatus
{
    /// <summary>No existing product matches the row's barcode — a new product will be created.</summary>
    public const string Create = "create";

    /// <summary>The row's barcode matches an existing product — that product will be updated in place.</summary>
    public const string Update = "update";

    /// <summary>The row failed validation; it is never applied on commit.</summary>
    public const string Error = "error";
}
