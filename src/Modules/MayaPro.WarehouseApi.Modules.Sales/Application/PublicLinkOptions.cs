namespace MayaPro.WarehouseApi.Modules.Sales.Application;

/// <summary>
/// Base URL the public invoice links are composed against (<c>App:PublicBaseUrl</c>). Separate from the
/// request host so links shared over WhatsApp point at the externally reachable address, not localhost
/// behind a proxy.
/// </summary>
public sealed record PublicLinkOptions(string PublicBaseUrl);
