namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>
/// The frozen column contract of the products Excel import template — the same idea as
/// <see cref="WireFormat"/>, but for a file instead of JSON. The Exports module <i>writes</i> these headers
/// into the downloadable template and the Products module <i>validates</i> an uploaded file against them, so
/// the two must never drift apart: a header edit here changes both sides at once, and the store user's saved
/// template keeps matching.
/// <para>
/// Column numbers are 1-based worksheet columns and are the positions in <see cref="Headers"/>, which is the
/// single ordered source of truth.
/// </para>
/// </summary>
public static class ProductImportTemplate
{
    // Header captions. A trailing "*" marks a required column (shown as such on the template's rules sheet).
    public const string NameHeader = "Ad*";
    public const string CategoryHeader = "Kateqoriya";
    public const string BarcodeHeader = "Barkod";
    public const string PurchasePriceHeader = "Alış qiyməti*";
    public const string SalePriceHeader = "Satış qiyməti*";
    public const string QuantityHeader = "Miqdar*";
    public const string MinStockHeader = "Min stok";
    public const string WarehouseHeader = "Anbar";
    public const string StoreHeader = "Mağaza";
    public const string ShelfHeader = "Rəf";
    public const string BoxHeader = "Qutu";
    public const string AttributesHeader = "Xüsusiyyətlər";
    public const string NoteHeader = "Qeyd";

    /// <summary>The header row, in worksheet column order.</summary>
    public static readonly IReadOnlyList<string> Headers =
    [
        NameHeader,
        CategoryHeader,
        BarcodeHeader,
        PurchasePriceHeader,
        SalePriceHeader,
        QuantityHeader,
        MinStockHeader,
        WarehouseHeader,
        StoreHeader,
        ShelfHeader,
        BoxHeader,
        AttributesHeader,
        NoteHeader
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

    /// <summary>How many data rows one uploaded file may carry — stated on the template's rules sheet and
    /// enforced by the import preview.</summary>
    public const int MaxDataRows = 1000;

    /// <summary>The attribute-cell format, e.g. <c>"Ölçü: M; Rəng: Qara"</c>, as shown to the user.</summary>
    public const string AttributesExample = "Ölçü: M; Rəng: Qara";
}
