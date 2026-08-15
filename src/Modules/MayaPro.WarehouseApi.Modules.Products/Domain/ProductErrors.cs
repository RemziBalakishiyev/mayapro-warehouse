using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Products.Domain;

/// <summary>
/// Business errors for the Products module. Messages are user-facing (Azerbaijani); the frontend shows
/// them directly. Codes drive the HTTP status via the shared Result → HTTP convention.
/// </summary>
public static class ProductErrors
{
    public static readonly Error NotFound =
        new("Products.NotFound", "Mal tapılmadı");

    public static readonly Error InsufficientStock =
        new("Products.InsufficientStock", "Stokda kifayət qədər mal yoxdur");

    /// <summary>
    /// Barcode generation was requested for a product that already has one. Code ends in
    /// <c>AlreadyExists</c> so the shared Result→HTTP convention maps it to 409.
    /// </summary>
    public static readonly Error BarcodeAlreadyExists =
        new("Products.BarcodeAlreadyExists", "Malın artıq barkodu var");

    /// <summary>
    /// A category with the same name already exists. Code deliberately does not end in
    /// <c>AlreadyExists</c>/<c>Conflict</c> so the shared Result→HTTP convention maps it to 400 (the agreed
    /// behaviour for a duplicate category), not 409.
    /// </summary>
    public static readonly Error CategoryDuplicate =
        new("Products.CategoryDuplicate", "Bu kateqoriya artıq mövcuddur");

    /// <summary>
    /// Another product in the same shop already carries this barcode. Until BE#35 this collision surfaced
    /// as a raw unique-index violation (a 500); it is now a business error, checked before the insert.
    /// Same code convention as <see cref="CategoryDuplicate"/> → 400, not 409.
    /// </summary>
    public static readonly Error BarcodeDuplicate =
        new("Products.BarcodeDuplicate", "Bu barkod artıq mövcuddur");
}
