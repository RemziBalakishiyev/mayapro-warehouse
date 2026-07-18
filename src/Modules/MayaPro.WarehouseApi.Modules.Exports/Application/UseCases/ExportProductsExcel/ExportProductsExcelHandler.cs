using ClosedXML.Excel;
using MayaPro.WarehouseApi.Modules.Exports.Application;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportProductsExcel;

/// <summary>
/// Builds the products catalogue Excel file from the Products / Suppliers / Settings contracts.
/// </summary>
public sealed class ExportProductsExcelHandler(
    IProductsModule products,
    ISuppliersModule suppliers,
    ISettingsModule settings,
    IDateProvider dateProvider)
{
    private static readonly string[] Headers =
    [
        "Ad",
        "Kateqoriya",
        "Xüsusiyyətlər",
        "Barkod",
        "Alış qiyməti",
        "Xərclər cəmi",
        "Real maya",
        "Satış qiyməti",
        "Qazanc %",
        "Stok",
        "Min stok",
        "Anbar yeri",
        "Status",
        "Təchizatçı"
    ];

    public async Task<ExportFileResult> Handle(CancellationToken ct)
    {
        string storeName = await settings.GetStoreNameAsync(ct);
        DateOnly today = dateProvider.Today;
        IReadOnlyList<ProductExportRow> rows = await products.GetExportProductsAsync(ct);

        var supplierIds = rows
            .Select(r => Guid.TryParse(r.SupplierId, out Guid id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct();
        Dictionary<Guid, string> supplierNames = await suppliers.GetNamesAsync(supplierIds, ct);

        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.Worksheets.Add("Mallar");

        sheet.Cell(1, 1).Value = $"{storeName} — {today:yyyy-MM-dd}";
        sheet.Range(1, 1, 1, Headers.Length).Merge();
        sheet.Cell(1, 1).Style.Font.Bold = true;

        for (int i = 0; i < Headers.Length; i++)
        {
            IXLCell header = sheet.Cell(2, i + 1);
            header.Value = Headers[i];
            header.Style.Font.Bold = true;
        }

        sheet.SheetView.FreezeRows(2);

        int excelRow = 3;
        foreach (ProductExportRow p in rows)
        {
            string supplierName = Guid.TryParse(p.SupplierId, out Guid sid) && supplierNames.TryGetValue(sid, out string? name)
                ? name
                : string.Empty;

            sheet.Cell(excelRow, 1).Value = p.Name;
            sheet.Cell(excelRow, 2).Value = p.Category;
            sheet.Cell(excelRow, 3).Value = p.AttributesText;
            sheet.Cell(excelRow, 4).Value = p.Barcode;
            SetMoney(sheet.Cell(excelRow, 5), p.PurchasePrice);
            SetMoney(sheet.Cell(excelRow, 6), p.ExpensesTotal);
            SetMoney(sheet.Cell(excelRow, 7), p.RealCostPerUnit);
            SetMoney(sheet.Cell(excelRow, 8), p.SalePrice);

            if (p.RealCostPerUnit > 0)
                sheet.Cell(excelRow, 9).Value = Math.Round(
                    (p.SalePrice - p.RealCostPerUnit) / p.RealCostPerUnit * 100m, 2);
            else
                sheet.Cell(excelRow, 9).Value = string.Empty;

            sheet.Cell(excelRow, 10).Value = p.Quantity;
            sheet.Cell(excelRow, 11).Value = p.MinStock;
            sheet.Cell(excelRow, 12).Value = p.Location;
            sheet.Cell(excelRow, 13).Value = ProductStatus.FromQuantity(p.Quantity, p.MinStock);
            sheet.Cell(excelRow, 14).Value = supplierName;
            excelRow++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        byte[] bytes = stream.ToArray();

        return new ExportFileResult(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"mallar-{today:yyyy-MM-dd}.xlsx");
    }

    private static void SetMoney(IXLCell cell, decimal value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = "#,##0.00";
    }
}
