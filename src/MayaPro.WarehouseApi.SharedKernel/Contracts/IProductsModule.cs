using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>
/// The Products module's public surface for other modules. Callers reach stock and cost through this
/// contract instead of touching the products tables.
/// </summary>
public interface IProductsModule
{
    /// <summary>
    /// Reserves <paramref name="quantity"/> units of a product for a sale. On success returns a snapshot
    /// (name + category + real cost) for the sale record. The change is made on the shared context but
    /// <b>not</b> saved — the caller commits it inside its own unit of work.
    /// </summary>
    Task<Result<ProductStockSnapshot>> TryDecreaseStockAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a product's current snapshot (name + real cost) without changing anything.</summary>
    Task<Result<ProductSnapshot>> GetSnapshotAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads every product's current snapshot. Used by the read-only Reports module to compute stock
    /// value and low-stock counts without touching the products tables.
    /// </summary>
    Task<IReadOnlyList<ProductSnapshot>> GetAllSnapshotsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a batch expense to a product's cost bucket, which recomputes its real cost. Used by the
    /// Expenses module when an expense is attached to a product. The change is made on the shared context
    /// but <b>not</b> saved — the caller commits it inside its own unit of work.
    /// </summary>
    Task<Result> AddExpenseToProductAsync(
        Guid productId,
        ProductCostBucket bucket,
        decimal amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of products linked to each supplier, keyed by the supplier's id. Products whose
    /// supplier reference is blank or not a valid id are omitted. Computed with a single grouped query.
    /// Used by the Suppliers module for each supplier's item count.
    /// </summary>
    Task<Dictionary<Guid, int>> GetCountBySupplierAsync(CancellationToken cancellationToken = default);
}

/// <summary>Snapshot of a product at sale time: its name, category and current real cost per unit.</summary>
public sealed record ProductStockSnapshot(string ProductName, string Category, decimal RealCostPerUnit);

/// <summary>
/// A read-only product snapshot. Carries enough for the Reports module to value stock and flag low
/// stock: identity, category, on-hand quantity, its reorder threshold, real cost and sale price.
/// </summary>
public sealed record ProductSnapshot(
    Guid Id,
    string Name,
    string Category,
    int Quantity,
    int MinStock,
    decimal RealCostPerUnit,
    decimal SalePrice);

/// <summary>
/// The buckets an expense can contribute to a product's real cost. A provider-neutral mirror of the
/// product cost breakdown, so callers (Expenses) map their own categories onto it without referencing
/// the Products domain.
/// </summary>
public enum ProductCostBucket
{
    Transport = 1,
    Labor = 2,
    Storage = 3,
    Packaging = 4,
    Other = 5
}
