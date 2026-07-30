using ClosedXML.Excel;
using MayaPro.WarehouseApi.Modules.Exports.Application;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportProductsTemplate;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Exports.Tests;

/// <summary>
/// Unit tests for <see cref="ExportProductsTemplateHandler"/>: the workbook shape (bold header + two sample
/// rows on the first sheet, a second "rules" sheet in Azerbaijani). Mirrors BE#13's AC-1.
/// </summary>
public sealed class ExportProductsTemplateHandlerTests
{
    [Fact]
    public async Task Returns_An_Xlsx_With_The_Expected_Content_Type_And_File_Name()
    {
        var handler = new ExportProductsTemplateHandler();

        ExportFileResult file = await handler.Handle(default);

        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            file.ContentType);
        Assert.Equal("mallar-sablon.xlsx", file.FileName);
        Assert.NotEmpty(file.Content);
    }

    [Fact]
    public async Task First_Sheet_Has_The_Bold_Header_Row_And_Two_Sample_Rows()
    {
        var handler = new ExportProductsTemplateHandler();
        ExportFileResult file = await handler.Handle(default);

        using var stream = new MemoryStream(file.Content);
        using var workbook = new XLWorkbook(stream);
        IXLWorksheet sheet = workbook.Worksheet(1);

        // The written header row is the shared contract the Products import validates against, verbatim —
        // asserting against the constant is what keeps the produced file and the accepted file in step.
        string[] written = Enumerable
            .Range(1, ProductImportTemplate.Headers.Count)
            .Select(column => sheet.Cell(ProductImportTemplate.HeaderRow, column).GetString())
            .ToArray();

        Assert.Equal(ProductImportTemplate.Headers, written);
        Assert.Equal("Ad*", written[0]); // the caption itself is part of the contract, not just its slot
        Assert.Equal("Qeyd", written[^1]);
        Assert.Empty(sheet.Cell(ProductImportTemplate.HeaderRow, written.Length + 1).GetString());
        Assert.True(sheet.Cell(1, 1).Style.Font.Bold);

        int lastRow = sheet.LastRowUsed()!.RowNumber();
        Assert.Equal(3, lastRow); // header + 2 sample rows
        Assert.False(string.IsNullOrWhiteSpace(sheet.Cell(2, 1).GetString()));
        Assert.False(string.IsNullOrWhiteSpace(sheet.Cell(3, 1).GetString()));
    }

    [Fact]
    public async Task Second_Sheet_Carries_The_Azerbaijani_Rules()
    {
        var handler = new ExportProductsTemplateHandler();
        ExportFileResult file = await handler.Handle(default);

        using var stream = new MemoryStream(file.Content);
        using var workbook = new XLWorkbook(stream);

        Assert.Equal(2, workbook.Worksheets.Count);
        IXLWorksheet rules = workbook.Worksheet(2);

        string allText = string.Join(
            " ",
            rules.RowsUsed().Select(r => r.Cell(1).GetString()));

        Assert.Contains("məcburidir", allText);
        Assert.Contains("Ölçü: M; Rəng: Qara", allText);
        Assert.Contains("1000", allText);
        Assert.Contains("yenilənməsi", allText); // existing-barcode = update rule
    }
}
