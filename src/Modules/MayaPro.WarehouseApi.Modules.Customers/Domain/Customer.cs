using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Customers.Domain;

/// <summary>
/// A customer with a running debt (outstanding balance). Behaviour-rich — debt only moves through
/// <see cref="IncreaseDebt"/> / <see cref="DecreaseDebt"/>, and can never go below zero.
/// </summary>
public sealed class Customer : Entity
{
    // EF Core constructor.
    private Customer() { }

    private Customer(string name, string? phone, string? note, decimal debt)
    {
        Name = name;
        Phone = phone;
        Note = note;
        Debt = debt;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Outstanding balance owed by the customer.</summary>
    public decimal Debt { get; private set; }

    public static Customer Create(string name, string? phone = null, string? note = null, decimal debt = 0) =>
        new(name, phone, note, debt);

    /// <summary>Adds to the debt (e.g. a credit sale).</summary>
    public void IncreaseDebt(decimal amount) => Debt += amount;

    /// <summary>
    /// Reduces the debt by a payment. Fails if the payment exceeds the outstanding debt — a customer
    /// cannot be pushed into credit.
    /// </summary>
    public Result DecreaseDebt(decimal amount)
    {
        if (amount > Debt)
            return Result.Failure(CustomerErrors.PaymentExceedsDebt);

        Debt -= amount;
        return Result.Success();
    }
}
