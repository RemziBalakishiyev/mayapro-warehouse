using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Settings.Domain;

/// <summary>
/// The store's configuration — a single-row (singleton) table. There is never more than one
/// <see cref="StoreSettings"/>; it is created lazily with sensible defaults on first read and then edited
/// in place. The fixed <see cref="SingletonId"/> keeps the row unique and idempotent to seed.
/// </summary>
public sealed class StoreSettings : Entity
{
    /// <summary>The one and only settings row's id.</summary>
    public static readonly Guid SingletonId = new("11111111-1111-1111-1111-111111111111");

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
        Id = SingletonId;
        StoreName = storeName;
        OwnerName = ownerName;
        WhatsappTemplate = whatsappTemplate;
        Currency = currency;
        DefaultMinStock = defaultMinStock;
        Language = language;
    }

    public string StoreName { get; private set; } = DefaultStoreName;

    public string? OwnerName { get; private set; }

    public string WhatsappTemplate { get; private set; } = DefaultWhatsappTemplate;

    public string Currency { get; private set; } = DefaultCurrency;

    /// <summary>Default reorder threshold applied to new products.</summary>
    public int DefaultMinStock { get; private set; } = DefaultMinStockValue;

    public string Language { get; private set; } = DefaultLanguage;

    /// <summary>Builds the singleton with default values, used on first access.</summary>
    public static StoreSettings CreateDefault() =>
        new(DefaultStoreName, null, DefaultWhatsappTemplate, DefaultCurrency, DefaultMinStockValue, DefaultLanguage);

    public void Update(
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
}
