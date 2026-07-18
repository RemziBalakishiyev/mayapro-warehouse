namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>The Suppliers module's public surface for other modules — debt totals and name lookups.</summary>
public interface ISuppliersModule
{
    /// <summary>
    /// Sums what we owe across all suppliers. Used by the read-only Reports module for the "my debts"
    /// figure on the dashboard.
    /// </summary>
    Task<decimal> GetTotalDebtAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves supplier display names for the given ids. Missing ids are omitted from the dictionary.
    /// Used by the Exports module to show the supplier column on the product Excel file.
    /// </summary>
    Task<Dictionary<Guid, string>> GetNamesAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);
}
