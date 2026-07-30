using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Exports.Tests;

/// <summary>
/// The host selects the QuestPDF licence when it registers the Exports module; unit tests drive the
/// handlers directly, so they have to make the same declaration before the first document is generated.
/// </summary>
internal static class QuestPdfLicenseInitializer
{
    [ModuleInitializer]
    internal static void Initialize() => QuestPDF.Settings.License = LicenseType.Community;
}
