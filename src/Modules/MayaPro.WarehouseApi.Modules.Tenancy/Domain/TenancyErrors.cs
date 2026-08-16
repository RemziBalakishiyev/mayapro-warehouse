using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Domain;

/// <summary>
/// Business errors for the Tenancy module (BE#36). Same convention as every other module: the code's suffix
/// picks the HTTP status (<c>NotFound</c> → 404, <c>AlreadyExists</c> → 409) and the message is the
/// user-facing Azerbaijani text the frontend shows verbatim.
/// </summary>
public static class TenancyErrors
{
    public static readonly Error TenantNotFound =
        new("Tenancy.TenantNotFound", "Mağaza tapılmadı");

    /// <summary>
    /// Registration is anonymous, so the phone is checked across <b>all</b> shops. A duplicate is a 409 —
    /// and it is what removes the "same phone in two shops" ambiguity from login (docs §4.1).
    /// </summary>
    public static readonly Error PhoneAlreadyExists =
        new("Tenancy.PhoneAlreadyExists", "Bu telefon nömrəsi artıq qeydiyyatdadır");
}
