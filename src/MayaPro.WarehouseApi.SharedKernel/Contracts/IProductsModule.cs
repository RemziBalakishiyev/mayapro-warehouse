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

    /// <summary>
    /// Returns <paramref name="quantity"/> units to a product's stock — the inverse of
    /// <see cref="TryDecreaseStockAsync"/>. Used when a sale is deleted or revised so the reserved stock is
    /// released. Fails if the product does not exist. The change is made on the shared context but
    /// <b>not</b> saved — the caller commits it inside its own unit of work.
    /// </summary>
    Task<Result> IncreaseStockAsync(
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
    /// Adds a named batch expense line to a product (same name accumulates), which recomputes its real
    /// cost. Used by the Expenses module when an expense is attached to a product — the caller's category
    /// name is passed through as the line name. The change is made on the shared context but <b>not</b>
    /// saved — the caller commits it inside its own unit of work.
    /// </summary>
    Task<Result> AddExpenseToProductAsync(
        Guid productId,
        string category,
        decimal amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a previously-added batch expense amount from a product (the inverse of
    /// <see cref="AddExpenseToProductAsync"/>), which recomputes its real cost back down. Used by the
    /// Expenses module when a product-linked expense is deleted or revised. Subtracts from the matching
    /// named line (case-insensitive), dropping the line once it reaches zero; an unknown line is a no-op.
    /// Fails if the product does not exist. The change is made on the shared context but <b>not</b> saved.
    /// </summary>
    Task<Result> RemoveExpenseFromProductAsync(
        Guid productId,
        string category,
        decimal amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of products linked to each supplier, keyed by the supplier's id. Products whose
    /// supplier reference is blank or not a valid id are omitted. Computed with a single grouped query.
    /// Used by the Suppliers module for each supplier's item count.
    /// </summary>
    Task<Dictionary<Guid, int>> GetCountBySupplierAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every product with the fields needed for the Excel catalogue export. Used by the
    /// read-only Exports module — richer than <see cref="ProductSnapshot"/> but still a contract DTO,
    /// not the Products API shape.
    /// </summary>
    Task<IReadOnlyList<ProductExportRow>> GetExportProductsAsync(CancellationToken cancellationToken = default);
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
/// A product row for Excel export: catalogue fields plus expenses total and supplier reference.
/// <see cref="AttributesText"/> is already formatted as <c>name: value; …</c> (empty attributes omitted).
/// </summary>
public sealed record ProductExportRow(
    Guid Id,
    string Name,
    string Category,
    string AttributesText,
    string Barcode,
    decimal PurchasePrice,
    decimal ExpensesTotal,
    decimal RealCostPerUnit,
    decimal SalePrice,
    int Quantity,
    int MinStock,
    string Location,
    string SupplierId);

