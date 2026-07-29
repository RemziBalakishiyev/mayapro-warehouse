using System.Globalization;
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
/// never assigns one. Repeating the same product in the request is allowed: each entry contributes its
/// own run of labels, and every copy counts towards the sheet-wide cap.
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

    // Pixel size of the rendered code image. Generous enough that a 300dpi printer never sees a scaled-up
    // bitmap (63mm at 300dpi ≈ 740px), small enough to keep the PDF light.
    private const int BarcodeImageWidthPx = 600;
    private const int BarcodeImageHeightPx = 160;
    private const int QrImageSizePx = 300;

    // A tiny safety cushion (points) so float rounding between the millimetre grid math and QuestPDF's own
    // (integer-rounded) A4 page size never makes the 3×8 grid a hair too wide/tall for the page.
    private const float SafetyCushionPt = 1f;

    private static readonly Error NoItems =
        new("Exports.NoLabelItems", "Ən azı bir mal seçilməlidir");

    private static readonly Error InvalidCount =
        new("Exports.InvalidLabelCount", "Etiket sayı ən azı 1 olmalıdır");

    private static readonly Error TooManyLabels =
        new("Exports.TooManyLabels", $"Bir dəfəyə ən çoxu {MaxLabels} etiket çap etmək olar");

    /// <summary>Millimetres → points (QuestPDF's native unit for every size/margin/padding call): 1in = 25.4mm = 72pt.</summary>
    private static float Mm(float millimetres) => millimetres * 72f / 25.4f;

    public async Task<Result<ExportFileResult>> Handle(LabelsPdfRequest? request, CancellationToken ct)
    {
        // A missing body, "items": null and "items": [] are all the same mistake to the user.
        IReadOnlyList<LabelItemRequest?> items = request?.Items ?? Array.Empty<LabelItemRequest?>();
        if (items.Count == 0)
            return Result.Failure<ExportFileResult>(NoItems);

        // A null element ("items": [null]) would otherwise blow up as a 500 further down.
        if (items.Any(i => i is null))
            return Result.Failure<ExportFileResult>(NoItems);

        List<LabelItemRequest> requested = items.Select(i => i!).ToList();
        if (requested.Any(i => i.Count <= 0))
            return Result.Failure<ExportFileResult>(InvalidCount);

        // Summed as long: individually valid counts can still overflow an int, and an OverflowException
        // here would be a 500 instead of the intended 400.
        long totalCount = requested.Sum(i => (long)i.Count);
        if (totalCount > MaxLabels)
            return Result.Failure<ExportFileResult>(TooManyLabels);

        List<Guid> productIds = requested.Select(i => i.ProductId).Distinct().ToList();
        IReadOnlyList<ProductLabelInfo> found = await products.GetLabelInfoAsync(productIds, ct);
        Dictionary<Guid, ProductLabelInfo> byId = found.ToDictionary(p => p.Id);

        var unknownIds = new List<string>();
        var noBarcodeNames = new List<string>();
        var labels = new List<LabelData>();

        foreach (LabelItemRequest item in requested)
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
                $"Bu mallar tapılmadı: {string.Join(", ", unknownIds.Distinct())}"));

        if (noBarcodeNames.Count > 0)
            return Result.Failure<ExportFileResult>(new Error(
                "Exports.ProductsWithoutBarcode",
                $"Bu malların barkodu yoxdur: {string.Join(", ", noBarcodeNames.Distinct())}"));

        // Anything other than an explicit "qr" prints the default Code128 barcode.
        bool useQr = string.Equals(request?.Type, "qr", StringComparison.OrdinalIgnoreCase);

        ExportFonts.EnsureRegistered();
        byte[] bytes = Render(labels, useQr);

        DateOnly today = dateProvider.Today;
        return Result.Success(new ExportFileResult(
            bytes,
            "application/pdf",
            $"etiketler-{today:yyyy-MM-dd}.pdf"));
    }

    private static byte[] Render(IReadOnlyList<LabelData> labels, bool useQr)
    {
        // One image per distinct barcode, not per label: printing 500 copies of one product must encode
        // the code once and reference the same embedded image 500 times, otherwise both the render time
        // and the PDF size grow with the copy count.
        Dictionary<string, Image> codeImages = labels
            .Select(l => l.Barcode)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                barcode => barcode,
                barcode => Image.FromBinaryData(useQr
                    ? LabelCodeImageRenderer.RenderQrCode(barcode, QrImageSizePx)
                    : LabelCodeImageRenderer.RenderBarcode(barcode, BarcodeImageWidthPx, BarcodeImageHeightPx)),
                StringComparer.Ordinal);

        try
        {
            // Margins are derived from QuestPDF's own (integer-point-rounded) A4 size, not a separate mm→pt
            // conversion, so the 3×8 grid always fits exactly the page it will actually be laid out on.
            PageSize pageSize = PageSizes.A4;
            float gridWidthPt = Columns * Mm(LabelWidthMm + GapMm);
            float gridHeightPt = Rows * Mm(LabelHeightMm + GapMm);
            float horizontalMarginPt = Math.Max(0f, (pageSize.Width - gridWidthPt) / 2f - SafetyCushionPt);
            float verticalMarginPt = Math.Max(0f, (pageSize.Height - gridHeightPt) / 2f - SafetyCushionPt);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(pageSize);
                    page.MarginHorizontal(horizontalMarginPt);
                    page.MarginVertical(verticalMarginPt);
                    page.DefaultTextStyle(x => x.FontFamily(ExportFonts.Family).FontSize(7));

                    page.Content().Element(c => ComposeGrid(c, labels, codeImages));
                });
            }).GeneratePdf();
        }
        finally
        {
            // QuestPDF images hold unmanaged Skia bitmaps — release them as soon as the document is built.
            foreach (Image image in codeImages.Values)
                image.Dispose();
        }
    }

    private static void ComposeGrid(
        IContainer container,
        IReadOnlyList<LabelData> labels,
        IReadOnlyDictionary<string, Image> codeImages)
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
                    {
                        LabelData label = row[i];
                        table.Cell().Element(e => ComposeLabel(e, label, codeImages[label.Barcode]));
                    }
                    else
                    {
                        table.Cell(); // trailing blank slot on the last row of the sheet
                    }
                }
            }
        });
    }

    private static void ComposeLabel(IContainer container, LabelData label, Image codeImage)
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
                col.Item().Text(TruncateName(label.Name)).FontSize(6.5f).LineHeight(1.05f).ClampLines(2);
                col.Item().PaddingTop(1).Text(FormatPrice(label.SalePrice)).Bold().FontSize(9);
                col.Item().PaddingTop(2).AlignCenter().Height(Mm(15)).Image(codeImage);
                col.Item().AlignCenter().PaddingTop(1).Text(label.Barcode).FontSize(6);
            });
    }

    /// <summary>
    /// Culture-independent on purpose: a price sticker must read <c>12.50 ₼</c> on every machine, whatever
    /// the server's regional settings happen to be.
    /// </summary>
    private static string FormatPrice(decimal salePrice) =>
        string.Create(CultureInfo.InvariantCulture, $"{salePrice:N2} ₼");

    /// <summary>
    /// Keeps the product name to roughly two lines within a 63mm label at the label's small font size —
    /// exact glyph metrics aren't worth measuring here, a conservative character cap plus the layout-level
    /// two-line clamp is enough.
    /// </summary>
    private static string TruncateName(string name)
    {
        const int maxLength = 40;
        return name.Length <= maxLength ? name : name[..(maxLength - 3)].TrimEnd() + "...";
    }

    private sealed record LabelData(string Name, string Barcode, decimal SalePrice);
}
