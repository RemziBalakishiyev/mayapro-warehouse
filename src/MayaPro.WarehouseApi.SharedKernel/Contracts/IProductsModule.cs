using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>
/// The Products module's public surface for other modules. Callers reach stock through this contract
/// instead of touching the products tables.
/// </summary>
public interface IProductsModule
{
    /// <summary>
    /// Reserves <paramref name="quantity"/> units of a product for a sale. On success returns a snapshot
    /// (name + real cost) for the sale record. The change is made on the shared context but <b>not</b>
    /// saved — the caller commits it inside its own unit of work.
    /// </summary>
    Task<Result<ProductStockSnapshot>> TryDecreaseStockAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default);
}

/// <summary>Snapshot of a product at sale time: its name and current real cost per unit.</summary>
public sealed record ProductStockSnapshot(string ProductName, decimal RealCostPerUnit);
