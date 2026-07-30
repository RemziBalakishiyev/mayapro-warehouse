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

/// <summary>
/// The full parse result cached under a preview's <c>importToken</c> for up to 10 minutes.
/// <para>
/// <paramref name="OwnerUserId"/> is the user the preview was issued to: only they may commit it, so a token
/// that leaks (logs, a shared screen) is useless to anyone else. Null only when the preview ran without an
/// identified user, which the authenticated endpoints never allow.
/// </para>
/// </summary>
public sealed record CachedImportResult(
    IReadOnlyList<CachedImportRow> Rows,
    IReadOnlyList<string> NewCategories,
    Guid? OwnerUserId);
