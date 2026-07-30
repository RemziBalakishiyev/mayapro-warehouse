using ClosedXML.Excel;
using MayaPro.WarehouseApi.Modules.Exports.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportProductsTemplate;

/// <summary>
/// Builds the blank Excel template a store uploads to <c>POST /api/imports/products/preview</c> (Products
/// module). Two sample rows show the expected shape; a second sheet spells out the rules in Azerbaijani.
/// <para>
/// Headers and the row limit come from the shared <see cref="ProductImportTemplate"/> contract, which the
/// Products module's import preview validates against — one source of truth, so the file this module writes
/// and the file that module accepts can never drift apart.
/// </para>
/// </summary>
public sealed class ExportProductsTemplateHandler
{
    private static readonly string[][] SampleRows =
    [
        [
            "Kişi cins şalvar", "Şalvar", "SDK1001234", "15", "25", "20", "5",
            "Anbar A", "Mərkəz mağaza", "3", "12", "Ölçü: M; Rəng: Qara", "Yeni kolleksiya"
        ],
        [
            "Qadın köynək", "Köynək", "", "8", "16", "30", "3",
            "Anbar A", "Mərkəz mağaza", "1", "4", "Ölçü: S; Rəng: Ağ", ""
        ]
    ];

    public Task<ExportFileResult> Handle(CancellationToken ct)
    {
        using var workbook = new XLWorkbook();

        IXLWorksheet dataSheet = workbook.Worksheets.Add("Mallar");
        for (int i = 0; i < ProductImportTemplate.Headers.Count; i++)
        {
            IXLCell header = dataSheet.Cell(ProductImportTemplate.HeaderRow, i + 1);
            header.Value = ProductImportTemplate.Headers[i];
            header.Style.Font.Bold = true;
        }

        for (int row = 0; row < SampleRows.Length; row++)
        for (int col = 0; col < SampleRows[row].Length; col++)
            dataSheet.Cell(row + 2, col + 1).Value = SampleRows[row][col];

        dataSheet.SheetView.FreezeRows(1);
        dataSheet.Columns().AdjustToContents();

        IXLWorksheet rulesSheet = workbook.Worksheets.Add("Qaydalar");
        string[] rules =
        [
            "Excel ilə mal idxalı — qaydalar",
            "",
            "* işarəli sütunlar məcburidir: Ad, Alış qiyməti, Satış qiyməti, Miqdar.",
            "Kateqoriya mövcud siyahıda yoxdursa, avtomatik yaradılacaq.",
            "Barkod boş buraxıla bilər — bu halda yeni mal barkodsuz əlavə olunur.",
            "Barkod mövcud bir mala uyğundursa, həmin sətir YENİ mal deyil, mövcud malın " +
            "yenilənməsi kimi tətbiq olunur (ad/qiymət/stok yenilənir).",
            "Xüsusiyyətlər sütunu \"Ad: Dəyər; Ad: Dəyər\" formatında yazılır, məsələn: " +
            $"\"{ProductImportTemplate.AttributesExample}\".",
            $"Bir faylda ən çoxu {ProductImportTemplate.MaxDataRows} sətir ola bilər.",
            "Faylı yükləməzdən əvvəl bu şablonu doldurun — sütun başlıqlarını dəyişməyin."
        ];

        for (int i = 0; i < rules.Length; i++)
        {
            IXLCell cell = rulesSheet.Cell(i + 1, 1);
            cell.Value = rules[i];
            if (i == 0)
                cell.Style.Font.Bold = true;
        }

        rulesSheet.Column(1).Width = 90;
        rulesSheet.Style.Alignment.WrapText = true;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        byte[] bytes = stream.ToArray();

        return Task.FromResult(new ExportFileResult(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "mallar-sablon.xlsx"));
    }
}
