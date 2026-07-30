using ClosedXML.Excel;
using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Imports;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.PreviewProductsImport;

/// <summary>
/// Parses an uploaded Excel file against the products import template — validates every row, classifies it
/// as <c>create</c>/<c>update</c>/<c>error</c>, and flags categories that do not exist yet. Nothing is
/// written to the database; the parse result is cached under a fresh <c>importToken</c> for
/// <see cref="Application.UseCases.CommitProductsImport.CommitProductsImportHandler"/> to apply later.
/// <para>
/// Takes a plain <see cref="Stream"/> rather than an <c>IFormFile</c>: the multipart details belong to the
/// endpoint, and the use case stays HTTP-agnostic (and trivially unit-testable from a byte array).
/// </para>
/// </summary>
public sealed class PreviewProductsImportHandler(
    IProductsDbContext db,
    IImportTokenCache cache,
    ICurrentUser currentUser)
{
    public async Task<Result<ImportPreviewResponse>> Handle(
        Stream? content,
        long lengthBytes,
        CancellationToken ct)
    {
        if (content is null || lengthBytes <= 0)
            return Result.Failure<ImportPreviewResponse>(ImportErrors.EmptyFile);

        // Checked before a single byte is buffered: an oversized workbook never reaches memory.
        if (lengthBytes > ImportTemplate.MaxFileBytes)
            return Result.Failure<ImportPreviewResponse>(ImportErrors.FileTooLarge);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(buffer);
        }
        catch
        {
            // Not a readable .xlsx at all — same user-facing outcome as a wrong template.
            return Result.Failure<ImportPreviewResponse>(ImportErrors.InvalidTemplate);
        }

        using (workbook)
        {
            IXLWorksheet? sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet is null || !HeadersMatch(sheet))
                return Result.Failure<ImportPreviewResponse>(ImportErrors.InvalidTemplate);

            // One past the limit is enough to reject the file, so an oversized sheet is never materialised.
            List<IXLRow> dataRows = sheet.RowsUsed()
                .Where(r => r.RowNumber() > ProductImportTemplate.HeaderRow && !ImportRowParser.IsBlank(r))
                .Take(ProductImportTemplate.MaxDataRows + 1)
                .ToList();

            if (dataRows.Count == 0)
                return Result.Failure<ImportPreviewResponse>(ImportErrors.EmptyFile);

            if (dataRows.Count > ProductImportTemplate.MaxDataRows)
                return Result.Failure<ImportPreviewResponse>(ImportErrors.TooManyRows);

            Dictionary<string, Guid> existingByBarcode = await LoadBarcodeIndexAsync(ct);
            var existingCategories = new HashSet<string>(
                await db.Categories.Select(c => c.Name).ToListAsync(ct),
                StringComparer.OrdinalIgnoreCase);

            var rows = new List<CachedImportRow>(dataRows.Count);
            var newCategories = new List<string>();
            var seenBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (IXLRow row in dataRows)
            {
                rows.Add(Classify(row, existingByBarcode, seenBarcodes, existingCategories, newCategories));
            }

            var summary = new ImportSummary(
                rows.Count(r => r.Status == ImportRowStatus.Create),
                rows.Count(r => r.Status == ImportRowStatus.Update),
                rows.Count(r => r.Status == ImportRowStatus.Error),
                newCategories);

            string token = cache.Store(new CachedImportResult(rows, newCategories, currentUser.UserId));

            IReadOnlyList<ImportRowResult> responseRows = rows
                .Select(r => new ImportRowResult(r.RowNumber, r.Status, r.Data, r.Error))
                .ToList();

            return Result.Success(new ImportPreviewResponse(token, responseRows, summary));
        }
    }

    /// <summary>
    /// Classifies one row: an error, an update of the product that already owns the barcode, or a create.
    /// Any category name that is neither in the database nor already flagged is appended to
    /// <paramref name="newCategories"/> (the list the commit will create).
    /// </summary>
    private static CachedImportRow Classify(
        IXLRow row,
        Dictionary<string, Guid> existingByBarcode,
        HashSet<string> seenBarcodes,
        HashSet<string> existingCategories,
        List<string> newCategories)
    {
        int rowNumber = row.RowNumber();
        (string? error, ImportRowData? data) = ImportRowParser.Parse(row);

        if (error is not null || data is null)
            return ErrorRow(rowNumber, error ?? "Sətir oxunmadı");

        bool hasBarcode = !string.IsNullOrWhiteSpace(data.Barcode);

        // One barcode, one row. Repeated in the file it is always a mistake: two new rows would collide on
        // the unique barcode index at commit time, and two update rows would silently let the last one win.
        if (hasBarcode && !seenBarcodes.Add(data.Barcode))
            return ErrorRow(rowNumber, "Barkod bu fayl daxilində təkrarlanır");

        Guid existingId = Guid.Empty;
        bool isUpdate = hasBarcode && existingByBarcode.TryGetValue(data.Barcode, out existingId);

        if (!string.IsNullOrWhiteSpace(data.Category) &&
            !existingCategories.Contains(data.Category) &&
            !newCategories.Contains(data.Category, StringComparer.OrdinalIgnoreCase))
        {
            newCategories.Add(data.Category);
        }

        return isUpdate
            ? new CachedImportRow(rowNumber, ImportRowStatus.Update, data, null, existingId)
            : new CachedImportRow(rowNumber, ImportRowStatus.Create, data, null, null);
    }

    private static CachedImportRow ErrorRow(int rowNumber, string error) =>
        new(rowNumber, ImportRowStatus.Error, null, error, null);

    /// <summary>
    /// Barcode → product id for every product that has a barcode. Built with <c>TryAdd</c> rather than
    /// <c>ToDictionary</c>: barcodes are unique under SQL Server's case-insensitive collation, but a
    /// case-sensitive store would otherwise turn two look-alike barcodes into a duplicate-key crash.
    /// </summary>
    private async Task<Dictionary<string, Guid>> LoadBarcodeIndexAsync(CancellationToken ct)
    {
        var rows = await db.Products
            .Where(p => p.Barcode != "")
            .Select(p => new { p.Barcode, p.Id })
            .ToListAsync(ct);

        var index = new Dictionary<string, Guid>(rows.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
            index.TryAdd(row.Barcode.Trim(), row.Id);

        return index;
    }

    private static bool HeadersMatch(IXLWorksheet sheet)
    {
        for (int i = 0; i < ProductImportTemplate.Headers.Count; i++)
        {
            string actual = sheet.Cell(ProductImportTemplate.HeaderRow, i + 1).GetString().Trim();
            if (!string.Equals(actual, ProductImportTemplate.Headers[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
