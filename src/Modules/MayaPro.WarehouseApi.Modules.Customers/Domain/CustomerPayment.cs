using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Customers.Domain;

/// <summary>A payment received against a customer's debt.</summary>
public sealed class CustomerPayment : Entity
{
    // EF Core constructor.
    private CustomerPayment() { }

    private CustomerPayment(Guid customerId, decimal amount, string? note, Guid? receivedByUserId, DateTime date)
    {
        CustomerId = customerId;
        Amount = amount;
        Note = note;
        ReceivedByUserId = receivedByUserId;
        Date = date;
    }

    public Guid CustomerId { get; private set; }

    public decimal Amount { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The user who took the payment (cross-module id; no FK).</summary>
    public Guid? ReceivedByUserId { get; private set; }

    public DateTime Date { get; private set; }

    public static CustomerPayment Create(Guid customerId, decimal amount, string? note, Guid? receivedByUserId) =>
        new(customerId, amount, note, receivedByUserId, DateTime.UtcNow);
}
