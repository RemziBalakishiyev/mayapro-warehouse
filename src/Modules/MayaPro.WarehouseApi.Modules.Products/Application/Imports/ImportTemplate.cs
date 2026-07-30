namespace MayaPro.WarehouseApi.Modules.Products.Application.Imports;

/// <summary>
/// The Excel import template's shape: header row + row-count limit. Internal — only the preview handler
/// and its tests need it.
/// <para>
/// <see cref="Headers"/> must stay byte-for-byte identical to the Exports module's
/// <c>ExportProductsTemplateHandler.Headers</c> (the file a user downloads and re-uploads). The two lists
/// are duplicated on purpose — the modules do not reference each other — so a header edit on either side
/// needs the matching edit on the other.
/// </para>
/// </summary>
internal static class ImportTemplate
{
    public static readonly string[] Headers =
    [
        "Ad*",
        "Kateqoriya",
        "Barkod",
        "Alış qiyməti*",
        "Satış qiyməti*",
        "Miqdar*",
        "Min stok",
        "Anbar",
        "Mağaza",
        "Rəf",
        "Qutu",
        "Xüsusiyyətlər",
        "Qeyd"
    ];

    public const int NameColumn = 1;
    public const int CategoryColumn = 2;
    public const int BarcodeColumn = 3;
    public const int PurchasePriceColumn = 4;
    public const int SalePriceColumn = 5;
    public const int QuantityColumn = 6;
    public const int MinStockColumn = 7;
    public const int WarehouseColumn = 8;
    public const int StoreColumn = 9;
    public const int ShelfColumn = 10;
    public const int BoxColumn = 11;
    public const int AttributesColumn = 12;
    public const int NoteColumn = 13;

    /// <summary>Header row is row 1; data starts at row 2.</summary>
    public const int HeaderRow = 1;

    public const int MaxDataRows = 1000;

    /// <summary>How long a preview's parse result stays claimable via its <c>importToken</c>.</summary>
    public static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(10);
}
