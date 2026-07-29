namespace MayaPro.WarehouseApi.Modules.Products.Domain;

/// <summary>
/// Builds candidate barcodes in the store's own format: <c>"SDK"</c> followed by 7 digits, e.g.
/// <c>SDK0417382</c>. Callers are responsible for retrying <see cref="NextCandidate"/> against the unique
/// index when a collision is possible — see <c>GenerateBarcodeHandler</c>.
/// </summary>
public static class BarcodeGenerator
{
    private const string Prefix = "SDK";
    private const int DigitCount = 7;
    private const int UpperBoundExclusive = 10_000_000; // 10^7 — keeps exactly 7 digits, zero-padded.

    /// <summary>A new random 7-digit candidate, prefixed with <see cref="Prefix"/>.</summary>
    public static string NextCandidate() =>
        Prefix + Random.Shared.Next(0, UpperBoundExclusive).ToString($"D{DigitCount}");
}
