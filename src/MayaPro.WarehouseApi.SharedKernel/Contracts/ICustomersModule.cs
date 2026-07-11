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

    /// <summary>
    /// Sums the outstanding debt across all customers. Used by the read-only Reports module for the
    /// receivables figure on the dashboard.
    /// </summary>
    Task<decimal> GetTotalDebtAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <paramref name="take"/> most recent customer payments, each with the customer's name,
    /// for the dashboard's activity feed.
    /// </summary>
    Task<IReadOnlyList<RecentPaymentInfo>> GetRecentPaymentsAsync(
        int take,
        CancellationToken cancellationToken = default);
}

/// <summary>A recent customer payment for the dashboard feed. Date is the business-zone (local) date.</summary>
public sealed record RecentPaymentInfo(Guid Id, DateOnly Date, string CustomerName, decimal Amount);
