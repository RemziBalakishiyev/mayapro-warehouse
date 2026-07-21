namespace MayaPro.WarehouseApi.Modules.Products.Domain;

/// <summary>
/// Helpers for the free-form product expense list stored on <see cref="Product"/>. Amounts are AZN.
/// Lines are only ever merged through <see cref="Add"/> (same name accumulates; unknown name appends).
/// </summary>
public static class ProductExpenses
{
    public static IReadOnlyList<ProductExpenseItem> Empty { get; } = Array.Empty<ProductExpenseItem>();

    public static decimal Total(IReadOnlyList<ProductExpenseItem> items) =>
        items.Sum(i => i.Amount);

    /// <summary>
    /// Returns a new list with <paramref name="amount"/> added to the line matching <paramref name="name"/>
    /// (case-insensitive), or a new line appended when no match exists.
    /// </summary>
    public static IReadOnlyList<ProductExpenseItem> Add(
        IReadOnlyList<ProductExpenseItem> items,
        string name,
        decimal amount)
    {
        string trimmed = name.Trim();
        var list = items.ToList();
        int index = list.FindIndex(i => string.Equals(i.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            list[index] = list[index] with { Amount = list[index].Amount + amount };
        else
            list.Add(new ProductExpenseItem(trimmed, amount));
        return list;
    }

    /// <summary>
    /// Returns a new list with <paramref name="amount"/> subtracted from the line matching
    /// <paramref name="name"/> (case-insensitive) — the inverse of <see cref="Add"/>. The line is dropped
    /// once its amount reaches zero or below; when no line matches the removal is a no-op (the list is
    /// returned unchanged), so unwinding an expense whose line was already edited away is always safe.
    /// </summary>
    public static IReadOnlyList<ProductExpenseItem> Remove(
        IReadOnlyList<ProductExpenseItem> items,
        string name,
        decimal amount)
    {
        string trimmed = name.Trim();
        var list = items.ToList();
        int index = list.FindIndex(i => string.Equals(i.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return list;

        decimal remaining = list[index].Amount - amount;
        if (remaining > 0m)
            list[index] = list[index] with { Amount = remaining };
        else
            list.RemoveAt(index);
        return list;
    }
}
