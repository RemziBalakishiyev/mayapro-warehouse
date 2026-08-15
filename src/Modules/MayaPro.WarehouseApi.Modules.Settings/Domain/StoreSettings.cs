using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Settings.Domain;

/// <summary>
/// A store's configuration — one row <b>per tenant</b>. It is created lazily with sensible defaults on
/// first read and then edited in place.
/// <para>
/// BE#35: this used to be a true singleton pinned to a fixed <c>SingletonId</c>. Under multi-tenancy the
/// uniqueness moved to the tenant: each shop gets its own row (new random id), the global query filter
/// makes "the settings row" unambiguous inside a request, and a unique index on <c>TenantId</c> keeps a
/// tenant from ever accumulating two. The legacy fixed id survives only as
/// <see cref="LegacySingletonId"/>, which the data migration reuses for the default tenant's row.
/// </para>
/// </summary>
public sealed class StoreSettings : TenantEntity
{
    /// <summary>
    /// The id the pre-multi-tenancy singleton row carried. Kept as a constant so the migration (and tests
    /// that assert the row survived) can still name it; new rows never use it.
    /// </summary>
    public static readonly Guid LegacySingletonId = new("11111111-1111-1111-1111-111111111111");

    public const string DefaultStoreName = "Sədərək Anbar";
    public const string DefaultCurrency = "AZN";
    public const string DefaultLanguage = "az";
    public const int DefaultMinStockValue = 10;

    /// <summary>
    /// Default WhatsApp debt-reminder template. The frontend recognises a single placeholder — {debt} —
    /// which it substitutes with the customer's outstanding balance before sending. Kept identical to the
    /// frontend's own default so switching the mock off changes nothing.
    /// </summary>
    public const string DefaultWhatsappTemplate =
        "Salam, sizdə {debt} AZN qalıq borc görünür. Zəhmət olmasa ödənişi tamamlayın.";

    // EF Core constructor.
    private StoreSettings() { }

    private StoreSettings(
        string storeName,
        string? ownerName,
        string whatsappTemplate,
        string currency,
        int defaultMinStock,
        string language)
    {
        StoreName = storeName;
        OwnerName = ownerName;
        WhatsappTemplate = whatsappTemplate;
        Currency = currency;
        DefaultMinStock = defaultMinStock;
        Language = language;
    }

    public string StoreName { get; private set; } = DefaultStoreName;

    public string? OwnerName { get; private set; }

    /// <summary>Store address printed on invoice headers; null hides the line.</summary>
    public string? Address { get; private set; }

    /// <summary>Store contact phone printed on invoice headers; null hides the line.</summary>
    public string? Phone { get; private set; }

    public string WhatsappTemplate { get; private set; } = DefaultWhatsappTemplate;

    public string Currency { get; private set; } = DefaultCurrency;

    /// <summary>Default reorder threshold applied to new products.</summary>
    public int DefaultMinStock { get; private set; } = DefaultMinStockValue;

    public string Language { get; private set; } = DefaultLanguage;

    /// <summary>Builds a tenant's settings row with default values, used on its first access.</summary>
    public static StoreSettings CreateDefault() =>
        new(DefaultStoreName, null, DefaultWhatsappTemplate, DefaultCurrency, DefaultMinStockValue, DefaultLanguage);

    public void Update(
        string storeName,
        string? ownerName,
        string? address,
        string? phone,
        string whatsappTemplate,
        string currency,
        int defaultMinStock,
        string language)
    {
        StoreName = storeName;
        OwnerName = ownerName;
        Address = address;
        Phone = phone;
        WhatsappTemplate = whatsappTemplate;
        Currency = currency;
        DefaultMinStock = defaultMinStock;
        Language = language;
    }
}
