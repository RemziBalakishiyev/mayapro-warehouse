namespace MayaPro.WarehouseApi.Modules.Products.Application.Imports;

/// <summary>
/// One parsed row as held server-side under the <c>importToken</c> — the preview response's
/// <see cref="ImportRowResult"/> plus the bits only <c>commit</c> needs (which existing product a
/// <c>update</c> row targets).
/// </summary>
public sealed record CachedImportRow(
    int RowNumber,
    string Status,
    ImportRowData? Data,
    string? Error,
    Guid? ExistingProductId);

/// <summary>The full parse result cached under a preview's <c>importToken</c> for up to 10 minutes.</summary>
public sealed record CachedImportResult(
    IReadOnlyList<CachedImportRow> Rows,
    IReadOnlyList<string> NewCategories);
