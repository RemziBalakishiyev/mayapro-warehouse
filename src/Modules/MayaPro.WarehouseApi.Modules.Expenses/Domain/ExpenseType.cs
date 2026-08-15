using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Expenses.Domain;

/// <summary>
/// A managed expense type (its own table in the <c>expenses</c> schema, unique by name). This is the
/// pick-list the UI offers when recording an expense; <see cref="Expense.Category"/> stays a plain string
/// snapshot, so renaming or deleting a type never rewrites existing expenses.
/// </summary>
public sealed class ExpenseType : TenantEntity
{
    // EF Core constructor.
    private ExpenseType() { }

    private ExpenseType(string name) => Name = name;

    public string Name { get; private set; } = string.Empty;

    public static ExpenseType Create(string name) => new(name.Trim());
}
