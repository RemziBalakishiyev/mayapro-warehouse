using MayaPro.WarehouseApi.SharedKernel.Application;

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
        new("Imports.TooManyRows", $"Bir faylda ən çoxu {ImportTemplate.MaxDataRows} sətir ola bilər");

    public static readonly Error InvalidTemplate =
        new("Imports.InvalidTemplate", "Şablona uyğun deyil — şablonu endirib istifadə et");

    /// <summary>The token was never issued (or is malformed) — never existed, as opposed to expired.</summary>
    public static readonly Error TokenNotFound =
        new("Imports.TokenNotFound", "Import vaxtı keçib — faylı yenidən yüklə");

    /// <summary>The token was issued but its 10-minute TTL has passed (or it was already committed once).</summary>
    public static readonly Error TokenExpired =
        new("Imports.TokenExpired", "Import vaxtı keçib — faylı yenidən yüklə");
}
