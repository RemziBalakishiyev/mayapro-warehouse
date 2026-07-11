using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Expenses.Domain;

/// <summary>
/// Expense categories. Persisted by name; the wire contract uses the frontend codes
/// (<c>EXP_CATS</c>: "Yol" | "Fəhlə" | "Anbar/Yer" | "Paket/Qutu" | "Mağaza" | "Digər").
/// </summary>
public enum ExpenseCategory
{
    Yol = 1,
    Fehle = 2,
    AnbarYer = 3,
    PaketQutu = 4,
    Magaza = 5,
    Diger = 6
}

/// <summary>
/// Maps <see cref="ExpenseCategory"/> to/from the frontend codes, and to the product cost bucket it
/// contributes to when the expense is attached to a product (the frontend <c>categoryToExpenseKey</c>).
/// </summary>
public static class ExpenseCategoryCode
{
    public const string Yol = "Yol";
    public const string Fehle = "Fəhlə";
    public const string AnbarYer = "Anbar/Yer";
    public const string PaketQutu = "Paket/Qutu";
    public const string Magaza = "Mağaza";
    public const string Diger = "Digər";

    public static string ToCode(this ExpenseCategory category) => category switch
    {
        ExpenseCategory.Yol => Yol,
        ExpenseCategory.Fehle => Fehle,
        ExpenseCategory.AnbarYer => AnbarYer,
        ExpenseCategory.PaketQutu => PaketQutu,
        ExpenseCategory.Magaza => Magaza,
        ExpenseCategory.Diger => Diger,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Naməlum xərc kateqoriyası")
    };

    public static bool TryParse(string? code, out ExpenseCategory category)
    {
        switch (code)
        {
            case Yol: category = ExpenseCategory.Yol; return true;
            case Fehle: category = ExpenseCategory.Fehle; return true;
            case AnbarYer: category = ExpenseCategory.AnbarYer; return true;
            case PaketQutu: category = ExpenseCategory.PaketQutu; return true;
            case Magaza: category = ExpenseCategory.Magaza; return true;
            case Diger: category = ExpenseCategory.Diger; return true;
            default: category = default; return false;
        }
    }

    /// <summary>
    /// The product cost bucket this category feeds. "Mağaza" (store rent) has no dedicated product bucket,
    /// so it — like "Digər" — falls into <see cref="ProductCostBucket.Diger"/>.
    /// </summary>
    public static ProductCostBucket ToCostBucket(this ExpenseCategory category) => category switch
    {
        ExpenseCategory.Yol => ProductCostBucket.Yol,
        ExpenseCategory.Fehle => ProductCostBucket.Fehle,
        ExpenseCategory.AnbarYer => ProductCostBucket.Yer,
        ExpenseCategory.PaketQutu => ProductCostBucket.Paket,
        ExpenseCategory.Magaza => ProductCostBucket.Diger,
        ExpenseCategory.Diger => ProductCostBucket.Diger,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Naməlum xərc kateqoriyası")
    };
}
