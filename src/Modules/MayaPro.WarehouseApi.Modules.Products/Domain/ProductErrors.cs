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
}
