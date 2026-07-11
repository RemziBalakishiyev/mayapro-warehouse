using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Domain;

/// <summary>
/// A supplier. <see cref="Debt"/> is what <i>we</i> owe them; it grows on a purchase and shrinks on a
/// payment, and can never go below zero.
/// </summary>
public sealed class Supplier : Entity
{
    // EF Core constructor.
    private Supplier() { }

    private Supplier(string name, string? contactName, string? phone, string? note, decimal debt)
    {
        Name = name;
        ContactName = contactName;
        Phone = phone;
        Note = note;
        Debt = debt;
    }

    public string Name { get; private set; } = string.Empty;

    public string? ContactName { get; private set; }

    public string? Phone { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Outstanding balance we owe the supplier.</summary>
    public decimal Debt { get; private set; }

    public static Supplier Create(
        string name, string? contactName = null, string? phone = null, string? note = null, decimal debt = 0) =>
        new(name, contactName, phone, note, debt);

    /// <summary>Adds to what we owe (a purchase on credit).</summary>
    public void IncreaseDebt(decimal amount) => Debt += amount;

    /// <summary>Reduces our debt by a payment. Fails if the payment exceeds the outstanding debt.</summary>
    public Result DecreaseDebt(decimal amount)
    {
        if (amount > Debt)
            return Result.Failure(SupplierErrors.PaymentExceedsDebt);

        Debt -= amount;
        return Result.Success();
    }
}
