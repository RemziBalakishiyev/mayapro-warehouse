using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Products.Application.Imports;

/// <summary>
/// Business errors for the Excel import flow (preview + commit). Codes ending in <c>TokenNotFound</c> /
/// <c>TokenExpired</c> map to 410 via the shared <c>ResultExtensions.StatusCodeFor</c> convention — see the
/// comment there.
/// </summary>
public static class ImportErrors
{
    public static readonly Error EmptyFile =
        new("Imports.EmptyFile", "Fayl boşdur — məlumat sətri tapılmadı");

    public static readonly Error TooManyRows =
        new("Imports.TooManyRows", $"Bir faylda ən çoxu {ProductImportTemplate.MaxDataRows} sətir ola bilər");

    public static readonly Error InvalidTemplate =
        new("Imports.InvalidTemplate", "Şablona uyğun deyil — şablonu endirib istifadə et");

    /// <summary>Guard against reading an oversized upload into memory — see <see cref="ImportTemplate.MaxFileBytes"/>.</summary>
    public static readonly Error FileTooLarge =
        new("Imports.FileTooLarge", $"Fayl çox böyükdür — ən çoxu {ImportTemplate.MaxFileBytes / (1024 * 1024)} MB");

    /// <summary>The token was never issued, is malformed, belongs to another user, or was already committed.</summary>
    public static readonly Error TokenNotFound =
        new("Imports.TokenNotFound", "Import vaxtı keçib — faylı yenidən yüklə");

    /// <summary>The token was issued but its 10-minute TTL has passed.</summary>
    public static readonly Error TokenExpired =
        new("Imports.TokenExpired", "Import vaxtı keçib — faylı yenidən yüklə");

    /// <summary>
    /// A barcode the preview classified as new belongs to a product that exists by the time the commit runs
    /// (someone else added it in between). Nothing is applied; the file has to be previewed again against
    /// the current catalogue.
    /// </summary>
    public static readonly Error BarcodeConflict =
        new("Imports.BarcodeConflict", "Fayldakı barkod artıq başqa mala aiddir — faylı yenidən yüklə");
}
