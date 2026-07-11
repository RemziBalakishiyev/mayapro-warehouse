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

    public async Task<Result<ProductSnapshot>> GetSnapshotAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        Product? product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        return product is null
            ? Result.Failure<ProductSnapshot>(ProductErrors.NotFound)
            : Result.Success(new ProductSnapshot(product.Id, product.Name, product.RealCostPerUnit));
    }

    public async Task<Result> AddExpenseToProductAsync(
        Guid productId,
        ProductCostBucket bucket,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        Product? product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
            return Result.Failure(ProductErrors.NotFound);

        product.AddExpense(ToKind(bucket), amount);
        return Result.Success();
    }

    private static ProductExpenseKind ToKind(ProductCostBucket bucket) => bucket switch
    {
        ProductCostBucket.Yol => ProductExpenseKind.Yol,
        ProductCostBucket.Fehle => ProductExpenseKind.Fehle,
        ProductCostBucket.Yer => ProductExpenseKind.Yer,
        ProductCostBucket.Paket => ProductExpenseKind.Paket,
        ProductCostBucket.Diger => ProductExpenseKind.Diger,
        _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Naməlum xərc növü")
    };
}
