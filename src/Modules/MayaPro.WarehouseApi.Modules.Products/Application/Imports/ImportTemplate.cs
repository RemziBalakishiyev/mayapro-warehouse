namespace MayaPro.WarehouseApi.Modules.Products.Application.Imports;

/// <summary>
/// Import-flow policy that belongs to this module alone: the upload ceiling, how long a preview stays
/// claimable, and the per-field caps a row is validated against.
/// <para>
/// The file's <i>shape</i> (header captions, column order, the 1000-row limit) is deliberately not here — it
/// is the shared <c>SharedKernel.Contracts.ProductImportTemplate</c> contract, written by the Exports
/// module's template endpoint and validated here, so the two sides can never drift apart.
/// </para>
/// </summary>
internal static class ImportTemplate
{
    /// <summary>How long a preview's parse result stays claimable via its <c>importToken</c>.</summary>
    public static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Upload ceiling. A filled-in 1000-row template is a few hundred KB, so 5 MB is generous while keeping
    /// a hostile or accidental multi-hundred-MB workbook from ever being read into memory.
    /// </summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Attribute-count cap, mirroring <c>CreateProductValidator</c> so a bulk import accepts exactly the
    /// products the single-product endpoint would.
    /// </summary>
    public const int MaxAttributes = 15;

    // Column length caps, mirroring ProductConfiguration / CategoryConfiguration. Checked during preview so
    // an over-long cell surfaces as a fixable row error instead of failing the whole commit transaction with
    // a database truncation error.
    public const int MaxNameLength = 200;
    public const int MaxCategoryLength = 100;
    public const int MaxBarcodeLength = 100;
    public const int MaxLocationPartLength = 100;
    public const int MaxLocationLength = 300;
}
