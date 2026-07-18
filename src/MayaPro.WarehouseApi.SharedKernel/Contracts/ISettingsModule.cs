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
}
