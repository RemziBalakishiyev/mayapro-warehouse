using System.Globalization;
using ClosedXML.Excel;
using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Products.Application.Imports;

/// <summary>
/// Parses one Excel data row into <see cref="ImportRowData"/>, or returns the first validation failure —
/// the row-level half of the preview handler, split out so it is unit-testable without a worksheet.
/// <para>
/// Every rule here also has to hold at commit time: what passes parsing is written to the database
/// unchanged, so the caps mirror <c>CreateProductValidator</c> and the column lengths in
/// <c>ProductConfiguration</c>.
/// </para>
/// </summary>
internal static class ImportRowParser
{
    /// <summary>
    /// True when the row carries nothing in any template column — a leftover formatted-but-empty row that
    /// Excel still reports as "used". Such rows are skipped instead of being reported as "Ad boşdur".
    /// </summary>
    public static bool IsBlank(IXLRow row)
    {
        for (int column = 1; column <= ProductImportTemplate.Headers.Count; column++)
        {
            if (!string.IsNullOrWhiteSpace(row.Cell(column).GetString()))
                return false;
        }

        return true;
    }

    public static (string? Error, ImportRowData? Data) Parse(IXLRow row)
    {
        string name = row.Cell(ProductImportTemplate.NameColumn).GetString().Trim();
        if (string.IsNullOrWhiteSpace(name))
            return ("Ad boşdur", null);

        if (name.Length > ImportTemplate.MaxNameLength)
            return ($"Ad {ImportTemplate.MaxNameLength} simvoldan uzun ola bilməz", null);

        if (!TryParseRequiredDecimal(
                row.Cell(ProductImportTemplate.PurchasePriceColumn), "Alış qiyməti",
                out decimal purchasePrice, out string? purchaseError))
            return (purchaseError, null);

        if (!TryParseRequiredDecimal(
                row.Cell(ProductImportTemplate.SalePriceColumn), "Satış qiyməti",
                out decimal salePrice, out string? saleError))
            return (saleError, null);

        if (!TryParseRequiredInt(
                row.Cell(ProductImportTemplate.QuantityColumn), "Miqdar",
                out int quantity, out string? quantityError))
            return (quantityError, null);

        if (!TryParseOptionalInt(
                row.Cell(ProductImportTemplate.MinStockColumn), "Min stok",
                out int minStock, out string? minStockError))
            return (minStockError, null);

        string category = row.Cell(ProductImportTemplate.CategoryColumn).GetString().Trim();
        string barcode = row.Cell(ProductImportTemplate.BarcodeColumn).GetString().Trim();
        string warehouse = row.Cell(ProductImportTemplate.WarehouseColumn).GetString().Trim();
        string store = row.Cell(ProductImportTemplate.StoreColumn).GetString().Trim();
        string shelf = row.Cell(ProductImportTemplate.ShelfColumn).GetString().Trim();
        string box = row.Cell(ProductImportTemplate.BoxColumn).GetString().Trim();
        string attributesText = row.Cell(ProductImportTemplate.AttributesColumn).GetString().Trim();
        string note = row.Cell(ProductImportTemplate.NoteColumn).GetString().Trim();

        string? tooLong =
            TooLong(category, ImportTemplate.MaxCategoryLength, "Kateqoriya") ??
            TooLong(barcode, ImportTemplate.MaxBarcodeLength, "Barkod") ??
            TooLong(warehouse, ImportTemplate.MaxLocationPartLength, "Anbar") ??
            TooLong(store, ImportTemplate.MaxLocationPartLength, "Mağaza") ??
            TooLong(shelf, ImportTemplate.MaxLocationPartLength, "Rəf") ??
            TooLong(box, ImportTemplate.MaxLocationPartLength, "Qutu");
        if (tooLong is not null)
            return (tooLong, null);

        List<ProductAttributeDto> attributes = ParseAttributes(attributesText);
        if (attributes.Count > ImportTemplate.MaxAttributes)
            return ($"Ən çoxu {ImportTemplate.MaxAttributes} xüsusiyyət əlavə etmək olar", null);

        var data = new ImportRowData(
            name,
            category,
            barcode,
            purchasePrice,
            salePrice,
            quantity,
            minStock,
            warehouse,
            store,
            shelf,
            box,
            attributes,
            note);

        return (null, data);
    }

    /// <summary>Parses <c>"Ölçü: M; Rəng: Qara"</c> into name/value pairs. Segments without a colon are skipped.</summary>
    public static List<ProductAttributeDto> ParseAttributes(string text)
    {
        var result = new List<ProductAttributeDto>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        foreach (string part in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = part.IndexOf(':');
            if (colon <= 0)
                continue;

            string attributeName = part[..colon].Trim();
            string attributeValue = part[(colon + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(attributeName))
                result.Add(new ProductAttributeDto(attributeName, attributeValue));
        }

        return result;
    }

    private static string? TooLong(string value, int max, string label) =>
        value.Length > max ? $"{label} {max} simvoldan uzun ola bilməz" : null;

    private static bool TryParseRequiredDecimal(IXLCell cell, string label, out decimal value, out string? error)
    {
        value = 0;
        if (IsEmptyCell(cell))
        {
            error = $"{label} boşdur";
            return false;
        }

        if (!TryReadDecimal(cell, out value))
        {
            error = $"{label} rəqəm deyil";
            return false;
        }

        if (value < 0)
        {
            error = $"{label} mənfi";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParseRequiredInt(IXLCell cell, string label, out int value, out string? error)
    {
        value = 0;
        if (IsEmptyCell(cell))
        {
            error = $"{label} boşdur";
            return false;
        }

        return TryReadWholeNumber(cell, label, out value, out error);
    }

    private static bool TryParseOptionalInt(IXLCell cell, string label, out int value, out string? error)
    {
        value = 0;
        error = null;
        return IsEmptyCell(cell) || TryReadWholeNumber(cell, label, out value, out error);
    }

    private static bool TryReadWholeNumber(IXLCell cell, string label, out int value, out string? error)
    {
        value = 0;

        if (!TryReadDecimal(cell, out decimal raw) || raw != Math.Truncate(raw))
        {
            error = $"{label} rəqəm deyil";
            return false;
        }

        if (raw < 0)
        {
            error = $"{label} mənfi";
            return false;
        }

        // Past int range there is no sane stock count; reported like any other unusable number.
        if (raw > int.MaxValue)
        {
            error = $"{label} çox böyükdür";
            return false;
        }

        value = (int)raw;
        error = null;
        return true;
    }

    /// <summary>A cell counts as empty when it holds nothing but whitespace, not only when Excel says so.</summary>
    private static bool IsEmptyCell(IXLCell cell) => string.IsNullOrWhiteSpace(cell.GetString());

    /// <summary>
    /// Reads a cell as a number whether it was typed as an Excel number or as text. Text is accepted both in
    /// the invariant form ("12.5") and in the Azerbaijani/European decimal-comma form ("12,5") — the store
    /// user's locale decides which one lands in the file, and both mean the same price.
    /// </summary>
    private static bool TryReadDecimal(IXLCell cell, out decimal value)
    {
        value = 0;

        // decimal (not double) so a price keeps its exact money value.
        if (cell.DataType == XLDataType.Number && cell.TryGetValue(out decimal number))
        {
            value = number;
            return true;
        }

        string raw = cell.GetString().Trim();
        if (raw.Length == 0)
            return false;

        // A lone comma is a decimal separator ("12,5"); a mixed "1,234.56" is invariant thousands grouping,
        // which the parse below already understands.
        if (raw.Contains(',') && !raw.Contains('.'))
            raw = raw.Replace(',', '.');

        const NumberStyles Styles = NumberStyles.Float | NumberStyles.AllowThousands;
        return decimal.TryParse(raw, Styles, CultureInfo.InvariantCulture, out value);
    }
}
