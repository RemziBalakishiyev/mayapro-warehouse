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
    /// A category with the same name already exists. Code deliberately does not end in
    /// <c>AlreadyExists</c>/<c>Conflict</c> so the shared Result→HTTP convention maps it to 400 (the agreed
    /// behaviour for a duplicate category), not 409.
    /// </summary>
    public static readonly Error CategoryDuplicate =
        new("Products.CategoryDuplicate", "Bu kateqoriya artıq mövcuddur");
}
