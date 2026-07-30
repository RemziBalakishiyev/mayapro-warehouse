using ClosedXML.Excel;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Products.Tests;

/// <summary>
/// Builds the byte[] of a small .xlsx the import tests upload. Headers come from the shared
/// <see cref="ProductImportTemplate"/> contract, so a test file is always exactly what the Exports module's
/// template endpoint hands the user.
/// </summary>
internal static class ImportWorkbookBuilder
{
    /// <summary>A full 13-column, correctly-ordered row: Ad, Kateqoriya, Barkod, Alış, Satış, Miqdar, Min
    /// stok, Anbar, Mağaza, Rəf, Qutu, Xüsusiyyətlər, Qeyd.</summary>
    public static object?[] Row(
        string name = "Test malı",
        string category = "Test",
        string barcode = "",
        object? purchasePrice = null,
        object? salePrice = null,
        object? quantity = null,
        object? minStock = null,
        string warehouse = "Anbar A",
        string store = "Mərkəz",
        string shelf = "1",
        string box = "1",
        string attributes = "",
        string note = "") =>
        [
            name, category, barcode,
            purchasePrice ?? 10, salePrice ?? 20, quantity ?? 5, minStock ?? 1,
            warehouse, store, shelf, box, attributes, note
        ];

    /// <summary>
    /// A row Excel still counts as "used" (the cells exist) but that carries no value in any template
    /// column — what is left behind when a user clears a filled row instead of deleting it.
    /// </summary>
    public static object?[] BlankRow() =>
        Enumerable.Repeat<object?>(" ", ProductImportTemplate.Headers.Count).ToArray();

    public static byte[] Build(IEnumerable<object?[]> rows, IReadOnlyList<string>? headers = null)
    {
        headers ??= ProductImportTemplate.Headers;

        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.Worksheets.Add("Mallar");

        for (int i = 0; i < headers.Count; i++)
            sheet.Cell(ProductImportTemplate.HeaderRow, i + 1).Value = headers[i];

        int rowIndex = ProductImportTemplate.HeaderRow + 1;
        foreach (object?[] row in rows)
        {
            for (int c = 0; c < row.Length; c++)
                SetCell(sheet.Cell(rowIndex, c + 1), row[c]);
            rowIndex++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static byte[] BuildHeaderOnly(IReadOnlyList<string>? headers = null) => Build([], headers);

    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case string s:
                cell.Value = s;
                break;
            case int i:
                cell.Value = i;
                break;
            case decimal d:
                cell.Value = d;
                break;
            case double db:
                cell.Value = db;
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }
}
