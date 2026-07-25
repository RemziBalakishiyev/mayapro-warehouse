namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>
/// The Settings module's public surface for other modules — currently the store display name used on
/// export headers and similar cross-module read-only surfaces.
/// </summary>
public interface ISettingsModule
{
    /// <summary>
    /// Returns the store name. Ensures the singleton settings row exists (creating defaults on first
    /// read, same as the settings API), so callers never receive an empty name.
    /// </summary>
    Task<string> GetStoreNameAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the store's header details for printed documents (invoice header): name, optional
    /// address/phone and the display currency. Same singleton-materialising behaviour as
    /// <see cref="GetStoreNameAsync"/>.
    /// </summary>
    Task<StoreInfo> GetStoreInfoAsync(CancellationToken cancellationToken = default);
}

/// <summary>The store's printable identity: name plus optional contact lines and display currency.</summary>
public sealed record StoreInfo(string StoreName, string? Address, string? Phone, string Currency);
