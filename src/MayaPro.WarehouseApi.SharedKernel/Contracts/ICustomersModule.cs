using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>
/// The Customers module's public surface for other modules — currently just adjusting a customer's debt
/// as part of a credit sale.
/// </summary>
public interface ICustomersModule
{
    /// <summary>
    /// Increases a customer's outstanding debt by <paramref name="amount"/>. Fails if the customer does
    /// not exist. The change is made on the shared context but <b>not</b> saved — the caller commits it
    /// inside its own unit of work.
    /// </summary>
    Task<Result> IncreaseDebtAsync(Guid customerId, decimal amount, CancellationToken cancellationToken = default);
}
