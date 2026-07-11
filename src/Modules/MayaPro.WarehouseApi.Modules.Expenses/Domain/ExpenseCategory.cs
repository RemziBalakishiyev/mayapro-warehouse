using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Expenses.Domain;

/// <summary>
/// Expense categories. Persisted by name; the wire contract uses the frontend codes
/// (<c>EXP_CATS</c>: "Yol" | "Fəhlə" | "Anbar/Yer" | "Paket/Qutu" | "Mağaza" | "Digər").
/// </summary>
public enum ExpenseCategory
{
    Transport = 1,
    Labor = 2,
    Storage = 3,
    Packaging = 4,
    Store = 5,
    Other = 6
}

/// <summary>
/// Maps <see cref="ExpenseCategory"/> to/from the frontend codes, and to the product cost bucket it
/// contributes to when the expense is attached to a product (the frontend <c>categoryToExpenseKey</c>).
/// The code values live in <see cref="WireFormat"/> (single source of truth).
/// </summary>
public static class ExpenseCategoryCode
{
    public const string Transport = WireFormat.ExpenseCategories.Transport;
    public const string Labor = WireFormat.ExpenseCategories.Labor;
    public const string Storage = WireFormat.ExpenseCategories.Storage;
    public const string Packaging = WireFormat.ExpenseCategories.Packaging;
    public const string Store = WireFormat.ExpenseCategories.Store;
    public const string Other = WireFormat.ExpenseCategories.Other;

    public static string ToCode(this ExpenseCategory category) => category switch
    {
        ExpenseCategory.Transport => Transport,
        ExpenseCategory.Labor => Labor,
        ExpenseCategory.Storage => Storage,
        ExpenseCategory.Packaging => Packaging,
        ExpenseCategory.Store => Store,
        ExpenseCategory.Other => Other,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Naməlum xərc kateqoriyası")
    };

    public static bool TryParse(string? code, out ExpenseCategory category)
    {
        switch (code)
        {
            case Transport: category = ExpenseCategory.Transport; return true;
            case Labor: category = ExpenseCategory.Labor; return true;
            case Storage: category = ExpenseCategory.Storage; return true;
            case Packaging: category = ExpenseCategory.Packaging; return true;
            case Store: category = ExpenseCategory.Store; return true;
            case Other: category = ExpenseCategory.Other; return true;
            default: category = default; return false;
        }
    }

    /// <summary>
    /// The product cost bucket this category feeds. "Mağaza" (store rent) has no dedicated product bucket,
    /// so it — like "Digər" — falls into <see cref="ProductCostBucket.Other"/>.
    /// </summary>
    public static ProductCostBucket ToCostBucket(this ExpenseCategory category) => category switch
    {
        ExpenseCategory.Transport => ProductCostBucket.Transport,
        ExpenseCategory.Labor => ProductCostBucket.Labor,
        ExpenseCategory.Storage => ProductCostBucket.Storage,
        ExpenseCategory.Packaging => ProductCostBucket.Packaging,
        ExpenseCategory.Store => ProductCostBucket.Other,
        ExpenseCategory.Other => ProductCostBucket.Other,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Naməlum xərc kateqoriyası")
    };
}
