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
        ExpenseCategory category,
        decimal amount,
        DateTime date,
        Guid? productId,
        string? productName,
        string? note,
        Guid? createdByUserId)
    {
        Name = name;
        Category = category;
        Amount = amount;
        Date = date;
        ProductId = productId;
        ProductName = productName;
        Note = note;
        CreatedByUserId = createdByUserId;
    }

    public string Name { get; private set; } = string.Empty;

    public ExpenseCategory Category { get; private set; }

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
        ExpenseCategory category,
        decimal amount,
        DateTime date,
        Guid? productId,
        string? productName,
        string? note,
        Guid? createdByUserId) =>
        new(name, category, amount, date, productId, productName, note, createdByUserId);
}
