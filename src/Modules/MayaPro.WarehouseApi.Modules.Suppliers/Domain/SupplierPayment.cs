using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Domain;

/// <summary>A payment we made against a supplier's debt.</summary>
public sealed class SupplierPayment : Entity
{
    // EF Core constructor.
    private SupplierPayment() { }

    private SupplierPayment(Guid supplierId, decimal amount, string? note, Guid? paidByUserId, DateTime date)
    {
        SupplierId = supplierId;
        Amount = amount;
        Note = note;
        PaidByUserId = paidByUserId;
        Date = date;
    }

    public Guid SupplierId { get; private set; }

    public decimal Amount { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The user who made the payment (cross-module id; no FK).</summary>
    public Guid? PaidByUserId { get; private set; }

    public DateTime Date { get; private set; }

    public static SupplierPayment Create(Guid supplierId, decimal amount, string? note, Guid? paidByUserId) =>
        new(supplierId, amount, note, paidByUserId, DateTime.UtcNow);
}
