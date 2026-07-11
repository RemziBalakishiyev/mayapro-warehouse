using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Application;

/// <summary>
/// The Products module's implementation of <see cref="IProductsModule"/>. Loads the product tracked (so
/// the decrement is part of the caller's unit of work), applies the domain rule, and returns a snapshot —
/// without saving.
/// </summary>
internal sealed class ProductsModuleContract(IProductsDbContext db) : IProductsModule
{
    public async Task<Result<ProductStockSnapshot>> TryDecreaseStockAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        Product? product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
            return Result.Failure<ProductStockSnapshot>(ProductErrors.NotFound);

        Result decrease = product.TryDecreaseStock(quantity);
        if (decrease.IsFailure)
            return Result.Failure<ProductStockSnapshot>(decrease.Error);

        return Result.Success(new ProductStockSnapshot(product.Name, product.RealCostPerUnit));
    }
}
