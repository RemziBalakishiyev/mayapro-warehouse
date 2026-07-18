using System.Reflection;
using QuestPDF.Drawing;

namespace MayaPro.WarehouseApi.Modules.Exports.Application;

/// <summary>
/// Registers embedded Noto Sans fonts (Regular + Bold) so Azerbaijani letters render in PDFs.
/// Safe to call more than once — registration is idempotent for our process lifetime.
/// </summary>
internal static class ExportFonts
{
    public const string Family = "Noto Sans";

    private static int _registered;

    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
            return;

        Assembly assembly = typeof(ExportFonts).Assembly;
        Register("MayaPro.WarehouseApi.Modules.Exports.Resources.Fonts.NotoSans-Regular.ttf", assembly);
        Register("MayaPro.WarehouseApi.Modules.Exports.Resources.Fonts.NotoSans-Bold.ttf", assembly);
    }

    private static void Register(string resourceName, Assembly assembly)
    {
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException($"Embedded font resource not found: {resourceName}");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        FontManager.RegisterFont(ms);
    }
}
