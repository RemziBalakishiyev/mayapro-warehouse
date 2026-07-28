using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Expenses.Domain;

/// <summary>
/// A business expense. May be attached to a product (<see cref="ProductId"/>), in which case it raises
/// that product's real cost; <see cref="ProductName"/> is a snapshot for display.
/// </summary>
public sealed class Expense : Entity
{
    // EF Core constructor.
    private Expense() { }

    private Expense(
        string name,
        string category,
        ExpenseSource source,
        decimal amount,
        DateTime date,
        Guid? productId,
        string? productName,
        string? note,
        Guid? createdByUserId)
    {
        Name = name;
        Category = category;
        Source = source;
        Amount = amount;
        Date = date;
        ProductId = productId;
        ProductName = productName;
        Note = note;
        CreatedByUserId = createdByUserId;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Free-form expense type name — a snapshot of the chosen <see cref="ExpenseType"/>'s name at the time
    /// the expense was recorded (see <see cref="ExpenseType"/> for the managed pick-list). Renaming or
    /// deleting a type never rewrites existing expenses.
    /// </summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>
    /// Whether this expense is attached to a product (<see cref="ExpenseSource.Product"/>, raises that
    /// product's real cost) or a general store expense with no product effect
    /// (<see cref="ExpenseSource.General"/>). Always consistent with <see cref="ProductId"/>.
    /// </summary>
    public ExpenseSource Source { get; private set; }

    public decimal Amount { get; private set; }

    public DateTime Date { get; private set; }

    /// <summary>The product this expense is attached to (cross-module id; no FK), or null for a general expense.</summary>
    public Guid? ProductId { get; private set; }

    /// <summary>Snapshot of the product's name at the time the expense was recorded.</summary>
    public string? ProductName { get; private set; }

    public string? Note { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public static Expense Create(
        string name,
        string category,
        ExpenseSource source,
        decimal amount,
        DateTime date,
        Guid? productId,
        string? productName,
        string? note,
        Guid? createdByUserId) =>
        new(name, category, source, amount, date, productId, productName, note, createdByUserId);

    /// <summary>
    /// Re-applies an expense's values in place after its old product-cost effect was reversed (the "reapply"
    /// half of an update). <paramref name="productName"/> is the freshly-resolved snapshot for the (possibly
    /// changed) product, or null for a general expense. Identity and creator are preserved.
    /// </summary>
    public void Update(
        string name,
        string category,
        ExpenseSource source,
        decimal amount,
        DateTime date,
        Guid? productId,
        string? productName,
        string? note)
    {
        Name = name;
        Category = category;
        Source = source;
        Amount = amount;
        Date = date;
        ProductId = productId;
        ProductName = productName;
        Note = note;
    }
}
