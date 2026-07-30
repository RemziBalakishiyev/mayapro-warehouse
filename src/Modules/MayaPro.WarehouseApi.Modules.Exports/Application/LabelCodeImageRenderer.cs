using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.Common;
using ZXing.ImageSharp.Rendering;

namespace MayaPro.WarehouseApi.Modules.Exports.Application;

/// <summary>
/// Renders a barcode/QR value to a PNG image (as raw bytes) via ZXing.Net, for embedding into a QuestPDF
/// label. Pure-managed (ImageSharp binding) — no native platform dependency.
/// </summary>
internal static class LabelCodeImageRenderer
{
    // Quiet zone (the mandatory blank run before the first bar and after the last one), in modules.
    // Without it a handheld scanner cannot find the code's edges, so the printed label would be useless.
    // 10 modules is the Code128 minimum; QR tolerates less than its nominal 4 and label space is scarce.
    private const int BarcodeQuietZoneModules = 10;
    private const int QrQuietZoneModules = 2;

    /// <summary>Renders <paramref name="value"/> as a Code128 barcode with a scanner-safe quiet zone.</summary>
    public static byte[] RenderBarcode(string value, int width, int height) =>
        Render(value, BarcodeFormat.CODE_128, width, height, BarcodeQuietZoneModules);

    /// <summary>Renders <paramref name="value"/> as a square QR code.</summary>
    public static byte[] RenderQrCode(string value, int size) =>
        Render(value, BarcodeFormat.QR_CODE, size, size, QrQuietZoneModules);

    private static byte[] Render(string value, BarcodeFormat format, int width, int height, int margin)
    {
        var writer = new ZXing.ImageSharp.BarcodeWriter<Rgba32>
        {
            Format = format,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = margin,
                PureBarcode = true // the human-readable digits are drawn by the PDF, not burned into the image
            },
            Renderer = new ImageSharpRenderer<Rgba32>()
        };

        using Image<Rgba32> image = writer.Write(value);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }
}
