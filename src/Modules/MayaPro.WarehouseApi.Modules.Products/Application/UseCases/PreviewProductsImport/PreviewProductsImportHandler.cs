using ClosedXML.Excel;
using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Imports;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.PreviewProductsImport;

/// <summary>
/// Parses an uploaded Excel file against the products import template — validates every row, classifies it
/// as <c>create</c>/<c>update</c>/<c>error</c>, and flags categories that do not exist yet. Nothing is
/// written to the database; the parse result is cached under a fresh <c>importToken</c> for
/// <see cref="Application.UseCases.CommitProductsImport.CommitProductsImportHandler"/> to apply later.
/// </summary>
public sealed class PreviewProductsImportHandler(IProductsDbContext db, IImportTokenCache cache)
{
    public async Task<Result<ImportPreviewResponse>> Handle(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return Result.Failure<ImportPreviewResponse>(ImportErrors.EmptyFile);

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
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

            List<IXLRow> dataRows = sheet.RowsUsed().Skip(1).ToList();

            if (dataRows.Count == 0)
                return Result.Failure<ImportPreviewResponse>(ImportErrors.EmptyFile);

            if (dataRows.Count > ImportTemplate.MaxDataRows)
                return Result.Failure<ImportPreviewResponse>(ImportErrors.TooManyRows);

            var existingProducts = await db.Products
                .Where(p => p.Barcode != "")
                .Select(p => new { p.Barcode, p.Id })
                .ToListAsync(ct);
            Dictionary<string, Guid> existingByBarcode = existingProducts
                .ToDictionary(p => p.Barcode, p => p.Id, StringComparer.OrdinalIgnoreCase);

            var existingCategories = new HashSet<string>(
                await db.Categories.Select(c => c.Name).ToListAsync(ct),
                StringComparer.OrdinalIgnoreCase);

            var cachedRows = new List<CachedImportRow>();
            var responseRows = new List<ImportRowResult>();
            var newCategories = new List<string>();
            var seenNewBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (IXLRow row in dataRows)
            {
                int rowNumber = row.RowNumber();
                (string? error, ImportRowData? data) = ImportRowParser.Parse(row);

                if (error is not null || data is null)
                {
                    cachedRows.Add(new CachedImportRow(rowNumber, ImportRowStatus.Error, null, error, null));
                    responseRows.Add(new ImportRowResult(rowNumber, ImportRowStatus.Error, null, error));
                    continue;
                }

                bool hasBarcode = !string.IsNullOrWhiteSpace(data.Barcode);
                Guid existingId = Guid.Empty;
                bool isUpdate = hasBarcode && existingByBarcode.TryGetValue(data.Barcode, out existingId);

                // A brand-new barcode repeated within the same file would otherwise create two products
                // that collide on the unique barcode index at commit time — caught here instead.
                if (!isUpdate && hasBarcode && !seenNewBarcodes.Add(data.Barcode))
                {
                    const string duplicateError = "Barkod bu fayl daxilində təkrarlanır";
                    cachedRows.Add(new CachedImportRow(rowNumber, ImportRowStatus.Error, null, duplicateError, null));
                    responseRows.Add(new ImportRowResult(rowNumber, ImportRowStatus.Error, null, duplicateError));
                    continue;
                }

                string status = isUpdate ? ImportRowStatus.Update : ImportRowStatus.Create;
                Guid? existingProductId = isUpdate ? existingId : null;

                if (!string.IsNullOrWhiteSpace(data.Category) &&
                    !existingCategories.Contains(data.Category) &&
                    !newCategories.Contains(data.Category, StringComparer.OrdinalIgnoreCase))
                {
                    newCategories.Add(data.Category);
                }

                cachedRows.Add(new CachedImportRow(rowNumber, status, data, null, existingProductId));
                responseRows.Add(new ImportRowResult(rowNumber, status, data, null));
            }

            int creates = cachedRows.Count(r => r.Status == ImportRowStatus.Create);
            int updates = cachedRows.Count(r => r.Status == ImportRowStatus.Update);
            int errors = cachedRows.Count(r => r.Status == ImportRowStatus.Error);

            string token = cache.Store(new CachedImportResult(cachedRows, newCategories));
            var summary = new ImportSummary(creates, updates, errors, newCategories);

            return Result.Success(new ImportPreviewResponse(token, responseRows, summary));
        }
    }

    private static bool HeadersMatch(IXLWorksheet sheet)
    {
        for (int i = 0; i < ImportTemplate.Headers.Length; i++)
        {
            string actual = sheet.Cell(ImportTemplate.HeaderRow, i + 1).GetString().Trim();
            if (!string.Equals(actual, ImportTemplate.Headers[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
