using System.Globalization;
using ClosedXML.Excel;
using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;

namespace MayaPro.WarehouseApi.Modules.Products.Application.Imports;

/// <summary>
/// Parses one Excel data row into <see cref="ImportRowData"/>, or returns the first validation failure —
/// the row-level half of the preview handler, split out so it is unit-testable without a worksheet.
/// </summary>
internal static class ImportRowParser
{
    public static (string? Error, ImportRowData? Data) Parse(IXLRow row)
    {
        string name = row.Cell(ImportTemplate.NameColumn).GetString().Trim();
        if (string.IsNullOrWhiteSpace(name))
            return ("Ad boşdur", null);

        if (!TryParseRequiredDecimal(row.Cell(ImportTemplate.PurchasePriceColumn), "Alış qiyməti", out decimal purchasePrice, out string? purchaseError))
            return (purchaseError, null);

        if (!TryParseRequiredDecimal(row.Cell(ImportTemplate.SalePriceColumn), "Satış qiyməti", out decimal salePrice, out string? saleError))
            return (saleError, null);

        if (!TryParseRequiredInt(row.Cell(ImportTemplate.QuantityColumn), "Miqdar", out int quantity, out string? quantityError))
            return (quantityError, null);

        if (!TryParseOptionalInt(row.Cell(ImportTemplate.MinStockColumn), "Min stok", out int minStock, out string? minStockError))
            return (minStockError, null);

        string category = row.Cell(ImportTemplate.CategoryColumn).GetString().Trim();
        string barcode = row.Cell(ImportTemplate.BarcodeColumn).GetString().Trim();
        string warehouse = row.Cell(ImportTemplate.WarehouseColumn).GetString().Trim();
        string store = row.Cell(ImportTemplate.StoreColumn).GetString().Trim();
        string shelf = row.Cell(ImportTemplate.ShelfColumn).GetString().Trim();
        string box = row.Cell(ImportTemplate.BoxColumn).GetString().Trim();
        string attributesText = row.Cell(ImportTemplate.AttributesColumn).GetString().Trim();
        string note = row.Cell(ImportTemplate.NoteColumn).GetString().Trim();

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
            ParseAttributes(attributesText),
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

    private static bool TryParseRequiredDecimal(IXLCell cell, string label, out decimal value, out string? error)
    {
        value = 0;
        if (cell.IsEmpty())
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
        if (cell.IsEmpty())
        {
            error = $"{label} boşdur";
            return false;
        }

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

        value = (int)raw;
        error = null;
        return true;
    }

    private static bool TryParseOptionalInt(IXLCell cell, string label, out int value, out string? error)
    {
        value = 0;
        error = null;
        if (cell.IsEmpty())
            return true;

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

        value = (int)raw;
        return true;
    }

    /// <summary>Reads a cell as a number whether it was entered as an Excel number or as text.</summary>
    private static bool TryReadDecimal(IXLCell cell, out decimal value)
    {
        if (cell.DataType == XLDataType.Number)
        {
            value = (decimal)cell.GetValue<double>();
            return true;
        }

        string raw = cell.GetString().Trim();
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
