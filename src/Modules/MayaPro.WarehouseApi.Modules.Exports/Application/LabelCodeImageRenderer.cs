using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.Common;
using ZXing.ImageSharp;
using ZXing.ImageSharp.Rendering;

namespace MayaPro.WarehouseApi.Modules.Exports.Application;

/// <summary>
/// Renders a barcode/QR value to a PNG image (as raw bytes) via ZXing.Net, for embedding into a QuestPDF
/// label. Pure-managed (ImageSharp binding) — no native platform dependency.
/// </summary>
internal static class LabelCodeImageRenderer
{
    /// <summary>Renders <paramref name="value"/> as a Code128 barcode, white background, no quiet-zone margin.</summary>
    public static byte[] RenderBarcode(string value, int width, int height) =>
        Render(value, BarcodeFormat.CODE_128, width, height);

    /// <summary>Renders <paramref name="value"/> as a square QR code.</summary>
    public static byte[] RenderQrCode(string value, int size) =>
        Render(value, BarcodeFormat.QR_CODE, size, size);

    private static byte[] Render(string value, BarcodeFormat format, int width, int height)
    {
        var writer = new ZXing.ImageSharp.BarcodeWriter<Rgba32>
        {
            Format = format,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 0,
                PureBarcode = true
            },
            Renderer = new ImageSharpRenderer<Rgba32>()
        };

        using Image<Rgba32> image = writer.Write(value);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }
}
