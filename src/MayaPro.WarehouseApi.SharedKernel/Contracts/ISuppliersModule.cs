namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>The Suppliers module's public surface for other modules — currently our total debt to suppliers.</summary>
public interface ISuppliersModule
{
    /// <summary>
    /// Sums what we owe across all suppliers. Used by the read-only Reports module for the "my debts"
    /// figure on the dashboard.
    /// </summary>
    Task<decimal> GetTotalDebtAsync(CancellationToken cancellationToken = default);
}
