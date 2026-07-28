using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Expenses.Domain;

/// <summary>
/// Where an expense's money went: attached to a product (raises its real cost) or a general store
/// expense with no product effect. Persisted by name; the wire contract uses <see cref="WireFormat.ExpenseSources"/>
/// ("general" | "product").
/// </summary>
public enum ExpenseSource
{
    General = 1,
    Product = 2
}

/// <summary>
/// Maps <see cref="ExpenseSource"/> to/from the frontend codes. The code values live in
/// <see cref="WireFormat"/> (single source of truth).
/// </summary>
public static class ExpenseSourceCode
{
    public const string General = WireFormat.ExpenseSources.General;
    public const string Product = WireFormat.ExpenseSources.Product;

    public static string ToCode(this ExpenseSource source) => source switch
    {
        ExpenseSource.General => General,
        ExpenseSource.Product => Product,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Naməlum xərc mənbəyi")
    };

    public static bool TryParse(string? code, out ExpenseSource source)
    {
        switch (code)
        {
            case General: source = ExpenseSource.General; return true;
            case Product: source = ExpenseSource.Product; return true;
            default: source = default; return false;
        }
    }
}
