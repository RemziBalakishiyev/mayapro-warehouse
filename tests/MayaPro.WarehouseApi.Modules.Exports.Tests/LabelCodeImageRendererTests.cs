using MayaPro.WarehouseApi.Modules.Exports.Application;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;

namespace MayaPro.WarehouseApi.Modules.Exports.Tests;

/// <summary>
/// The label codes are only worth printing if a scanner can read them back, so these tests decode what
/// the renderer produced instead of asserting on image bytes.
/// </summary>
public sealed class LabelCodeImageRendererTests
{
    private const string Barcode = "SDK0417382";

    [Fact]
    public void Barcode_Image_Decodes_Back_To_The_Value_As_Code128()
    {
        byte[] png = LabelCodeImageRenderer.RenderBarcode(Barcode, 600, 160);

        Result decoded = Decode(png);

        Assert.NotNull(decoded);
        Assert.Equal(Barcode, decoded.Text);
        Assert.Equal(BarcodeFormat.CODE_128, decoded.BarcodeFormat);
    }

    [Fact]
    public void Qr_Image_Decodes_Back_To_The_Value_As_Qr()
    {
        byte[] png = LabelCodeImageRenderer.RenderQrCode(Barcode, 300);

        Result decoded = Decode(png);

        Assert.NotNull(decoded);
        Assert.Equal(Barcode, decoded.Text);
        Assert.Equal(BarcodeFormat.QR_CODE, decoded.BarcodeFormat);
    }

    /// <summary>
    /// A Code128 symbol without the blank run on both sides is unreadable on paper, so the rendered image
    /// must keep its quiet zone: the first and last pixel columns stay white.
    /// </summary>
    [Fact]
    public void Barcode_Image_Keeps_A_Quiet_Zone_On_Both_Sides()
    {
        byte[] png = LabelCodeImageRenderer.RenderBarcode(Barcode, 600, 160);

        using Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(png);
        int middleRow = image.Height / 2;

        Assert.Equal(new Rgba32(255, 255, 255), image[0, middleRow]);
        Assert.Equal(new Rgba32(255, 255, 255), image[image.Width - 1, middleRow]);
    }

    [Fact]
    public void Barcode_Image_Honours_The_Requested_Pixel_Size()
    {
        byte[] png = LabelCodeImageRenderer.RenderBarcode(Barcode, 600, 160);

        using Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(png);

        Assert.Equal(600, image.Width);
        Assert.Equal(160, image.Height);
    }

    private static Result Decode(byte[] png)
    {
        using Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(png);
        var reader = new ZXing.ImageSharp.BarcodeReader<Rgba32>
        {
            Options = { TryHarder = true, PureBarcode = true }
        };
        return reader.Decode(image);
    }
}
