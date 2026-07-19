using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Customers.Domain;

/// <summary>
/// A manual movement of a customer's debt that is neither a credit sale nor a payment. Today it records the
/// opening balance when a customer is migrated into the system (<see cref="Amount"/> = the debt they already
/// owed, <see cref="Note"/> = "İlkin borc (sistemə keçid)"), so the customer's history has a first row that
/// explains where the starting debt came from. Kept as its own tiny entity so that opening balance is an
/// auditable record (who entered it, when) rather than an unexplained number on the customer row.
/// </summary>
public sealed class CustomerDebtAdjustment : Entity
{
    /// <summary>Note stamped on the opening-balance adjustment written when a customer is created with a debt.</summary>
    public const string InitialDebtNote = "İlkin borc (sistemə keçid)";

    // EF Core constructor.
    private CustomerDebtAdjustment() { }

    private CustomerDebtAdjustment(Guid customerId, decimal amount, string? note, Guid? createdByUserId, DateTime date)
    {
        CustomerId = customerId;
        Amount = amount;
        Note = note;
        CreatedByUserId = createdByUserId;
        Date = date;
    }

    public Guid CustomerId { get; private set; }

    /// <summary>The debt amount recorded by this adjustment (always positive for an opening balance).</summary>
    public decimal Amount { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The user who entered the adjustment (cross-module id; no FK).</summary>
    public Guid? CreatedByUserId { get; private set; }

    public DateTime Date { get; private set; }

    public static CustomerDebtAdjustment Create(Guid customerId, decimal amount, string? note, Guid? createdByUserId) =>
        new(customerId, amount, note, createdByUserId, DateTime.UtcNow);
}
