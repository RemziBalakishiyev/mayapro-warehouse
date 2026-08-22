namespace MayaPro.WarehouseApi.SharedKernel.Domain;

/// <summary>
/// BE#48 — the single source of truth for the product's customer-facing brand name ("Anbarcı Az"). Every
/// place that used to hard-code "MayaPro" in user-facing text (the qaimə-faktura footer, fallback store
/// names, and so on) reads it from here instead, so a future rebrand touches this one constant and nothing
/// else.
/// <para>
/// <b>The barcode prefix ("SDK") deliberately does NOT follow this brand.</b> It comes from the original
/// store name ("Sədərək") and is already baked into every barcode label printed and stuck on physical stock
/// before this rebrand — see <c>BarcodeGenerator.Prefix</c>. Changing it here would make every existing
/// printed label unrecognisable to the scanner/lookup flow for no benefit, so the prefix stays "SDK" even
/// though the brand shown to the user is now "Anbarcı Az".
/// </para>
/// </summary>
public static class BrandInfo
{
    /// <summary>The product's customer-facing name, shown on printed documents and used as fallback text.</summary>
    public const string Name = "Anbarcı Az";
}
