namespace MayaPro.WarehouseApi.Modules.Products.Application.Imports;

/// <summary>One row of the preview response — <see cref="Status"/> is one of <see cref="ImportRowStatus"/>.</summary>
public sealed record ImportRowResult(
    int RowNumber,
    string Status,
    ImportRowData? Data,
    string? Error);

/// <summary>Row-count breakdown of a preview, plus the category names that do not exist yet.</summary>
public sealed record ImportSummary(
    int Creates,
    int Updates,
    int Errors,
    IReadOnlyList<string> NewCategories);

/// <summary>
/// Response for <c>POST /api/imports/products/preview</c>. <see cref="ImportToken"/> is handed back to
/// <c>POST /api/imports/products/commit</c> to apply exactly this parse result.
/// </summary>
public sealed record ImportPreviewResponse(
    string ImportToken,
    IReadOnlyList<ImportRowResult> Rows,
    ImportSummary Summary);
