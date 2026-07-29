using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportProductLabelsPdf;

/// <summary>
/// Builds a printable A4 sheet of barcode/QR labels (3 columns × 8 rows of ~63×34mm labels — a standard
/// sticker-sheet layout) for a batch of products. Every product in the request must already carry a
/// barcode (see the Products module's <c>generate-barcode</c> endpoint) — this handler only prints, it
/// never assigns one.
/// </summary>
public sealed class ExportProductLabelsPdfHandler(IProductsModule products, IDateProvider dateProvider)
{
    private const int MaxLabels = 500;
    private const int Columns = 3;
    private const int Rows = 8;

    // Physical label size (a common Avery-style A4 sticker sheet) plus the cut gap between labels.
    private const float LabelWidthMm = 63f;
    private const float LabelHeightMm = 34f;
    private const float GapMm = 2f;

    // A tiny safety cushion (points) so float rounding between the millimetre grid math and QuestPDF's own
    // (integer-rounded) A4 page size never makes the 3×8 grid a hair too wide/tall for the page.
    private const float SafetyCushionPt = 1f;

    private static readonly Error NoItems =
        new("Exports.NoLabelItems", "Ən azı bir mal seçilməlidir");

    private static readonly Error InvalidCount =
        new("Exports.InvalidLabelCount", "Etiket sayı ən azı 1 olmalıdır");

    /// <summary>Millimetres → points (QuestPDF's native unit for every size/margin/padding call): 1in = 25.4mm = 72pt.</summary>
    private static float Mm(float millimetres) => millimetres * 72f / 25.4f;

    public async Task<Result<ExportFileResult>> Handle(LabelsPdfRequest request, CancellationToken ct)
    {
        IReadOnlyList<LabelItemRequest> items = request.Items ?? Array.Empty<LabelItemRequest>();
        if (items.Count == 0)
            return Result.Failure<ExportFileResult>(NoItems);

        if (items.Any(i => i.Count <= 0))
            return Result.Failure<ExportFileResult>(InvalidCount);

        int totalCount = items.Sum(i => i.Count);
        if (totalCount > MaxLabels)
            return Result.Failure<ExportFileResult>(new Error(
                "Exports.TooManyLabels",
                $"Bir dəfəyə ən çoxu {MaxLabels} etiket çap etmək olar"));

        List<Guid> productIds = items.Select(i => i.ProductId).Distinct().ToList();
        IReadOnlyList<ProductLabelInfo> found = await products.GetLabelInfoAsync(productIds, ct);
        Dictionary<Guid, ProductLabelInfo> byId = found.ToDictionary(p => p.Id);

        var unknownIds = new List<string>();
        var noBarcodeNames = new List<string>();
        var labels = new List<LabelData>();

        foreach (LabelItemRequest item in items)
        {
            if (!byId.TryGetValue(item.ProductId, out ProductLabelInfo? info))
            {
                unknownIds.Add(item.ProductId.ToString());
                continue;
            }

            if (string.IsNullOrWhiteSpace(info.Barcode))
            {
                noBarcodeNames.Add(info.Name);
                continue;
            }

            for (int i = 0; i < item.Count; i++)
                labels.Add(new LabelData(info.Name, info.Barcode, info.SalePrice));
        }

        if (unknownIds.Count > 0)
            return Result.Failure<ExportFileResult>(new Error(
                "Exports.UnknownProducts",
                $"Bu mallar tapılmadı: {string.Join(", ", unknownIds)}"));

        if (noBarcodeNames.Count > 0)
            return Result.Failure<ExportFileResult>(new Error(
                "Exports.ProductsWithoutBarcode",
                $"Bu malların barkodu yoxdur: {string.Join(", ", noBarcodeNames)}"));

        bool useQr = string.Equals(request.Type, "qr", StringComparison.OrdinalIgnoreCase);

        ExportFonts.EnsureRegistered();

        // Margins are derived from QuestPDF's own (integer-point-rounded) A4 size, not a separate mm→pt
        // conversion, so the 3×8 grid always fits exactly the page it will actually be laid out on.
        PageSize pageSize = PageSizes.A4;
        float gridWidthPt = Columns * Mm(LabelWidthMm + GapMm);
        float gridHeightPt = Rows * Mm(LabelHeightMm + GapMm);
        float horizontalMarginPt = Math.Max(0f, (pageSize.Width - gridWidthPt) / 2f - SafetyCushionPt);
        float verticalMarginPt = Math.Max(0f, (pageSize.Height - gridHeightPt) / 2f - SafetyCushionPt);

        byte[] bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(pageSize);
                page.MarginHorizontal(horizontalMarginPt);
                page.MarginVertical(verticalMarginPt);
                page.DefaultTextStyle(x => x.FontFamily(ExportFonts.Family).FontSize(7));

                page.Content().Element(c => ComposeGrid(c, labels, useQr));
            });
        }).GeneratePdf();

        DateOnly today = dateProvider.Today;
        return Result.Success(new ExportFileResult(
            bytes,
            "application/pdf",
            $"etiketler-{today:yyyy-MM-dd}.pdf"));
    }

    private static void ComposeGrid(IContainer container, IReadOnlyList<LabelData> labels, bool useQr)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                for (int i = 0; i < Columns; i++)
                    columns.ConstantColumn(Mm(LabelWidthMm + GapMm));
            });

            foreach (LabelData[] row in labels.Chunk(Columns))
            {
                for (int i = 0; i < Columns; i++)
                {
                    if (i < row.Length)
                        table.Cell().Element(e => ComposeLabel(e, row[i], useQr));
                    else
                        table.Cell(); // trailing blank slot on the last row of the sheet
                }
            }
        });
    }

    private static void ComposeLabel(IContainer container, LabelData label, bool useQr)
    {
        container
            .PaddingRight(Mm(GapMm))
            .PaddingBottom(Mm(GapMm))
            .Height(Mm(LabelHeightMm))
            .Border(0.5f)
            .BorderColor(Colors.Grey.Lighten1)
            .Padding(3)
            .Column(col =>
            {
                col.Item().Text(TruncateName(label.Name)).FontSize(6.5f).LineHeight(1.05f);
                col.Item().PaddingTop(1).Text($"{label.SalePrice:N2} ₼").Bold().FontSize(9);
                col.Item().PaddingTop(2).AlignCenter().Height(Mm(15))
                    .Image(useQr
                        ? LabelCodeImageRenderer.RenderQrCode(label.Barcode, 300)
                        : LabelCodeImageRenderer.RenderBarcode(label.Barcode, 600, 160));
                col.Item().AlignCenter().PaddingTop(1).Text(label.Barcode).FontSize(6);
            });
    }

    /// <summary>
    /// Keeps the product name to roughly two lines within a 63mm label at the label's small font size —
    /// exact glyph metrics aren't worth measuring here, a conservative character cap is enough.
    /// </summary>
    private static string TruncateName(string name)
    {
        const int maxLength = 40;
        return name.Length <= maxLength ? name : name[..(maxLength - 3)].TrimEnd() + "...";
    }

    private sealed record LabelData(string Name, string Barcode, decimal SalePrice);
}
