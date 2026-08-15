using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Domain;

/// <summary>
/// A manual movement of a supplier's debt that is neither a credit purchase nor a payment. Today it records
/// the opening balance when a supplier is migrated into the system (<see cref="Amount"/> = the debt we
/// already owed them, <see cref="Note"/> = "İlkin borc (sistemə keçid)"), so the supplier's history has a
/// first row that explains where the starting debt came from. Kept as its own tiny entity so that opening
/// balance is an auditable record (who entered it, when) rather than an unexplained number on the supplier
/// row.
/// </summary>
public sealed class SupplierDebtAdjustment : TenantEntity
{
    /// <summary>Note stamped on the opening-balance adjustment written when a supplier is created with a debt.</summary>
    public const string InitialDebtNote = "İlkin borc (sistemə keçid)";

    // EF Core constructor.
    private SupplierDebtAdjustment() { }

    private SupplierDebtAdjustment(Guid supplierId, decimal amount, string? note, Guid? createdByUserId, DateTime date)
    {
        SupplierId = supplierId;
        Amount = amount;
        Note = note;
        CreatedByUserId = createdByUserId;
        Date = date;
    }

    public Guid SupplierId { get; private set; }

    /// <summary>The debt amount recorded by this adjustment (always positive for an opening balance).</summary>
    public decimal Amount { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The user who entered the adjustment (cross-module id; no FK).</summary>
    public Guid? CreatedByUserId { get; private set; }

    public DateTime Date { get; private set; }

    public static SupplierDebtAdjustment Create(Guid supplierId, decimal amount, string? note, Guid? createdByUserId) =>
        new(supplierId, amount, note, createdByUserId, DateTime.UtcNow);
}
