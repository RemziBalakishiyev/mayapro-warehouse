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

        return Result.Success(new ProductStockSnapshot(product.Name, product.Category, product.RealCostPerUnit));
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
            : Result.Success(ToSnapshot(product));
    }

    public async Task<IReadOnlyList<ProductSnapshot>> GetAllSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        List<Product> products = await db.Products
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return products.Select(ToSnapshot).ToList();
    }

    private static ProductSnapshot ToSnapshot(Product product) => new(
        product.Id,
        product.Name,
        product.Category,
        product.Quantity,
        product.MinStock,
        product.RealCostPerUnit,
        product.SalePrice);

    public async Task<Result> AddExpenseToProductAsync(
        Guid productId,
        string category,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        Product? product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
            return Result.Failure(ProductErrors.NotFound);

        product.AddExpense(category, amount);
        return Result.Success();
    }

    public async Task<Dictionary<Guid, int>> GetCountBySupplierAsync(CancellationToken cancellationToken = default)
    {
        // Group by the string supplier reference in SQL (a single query); Product.SupplierId is a loose
        // cross-module string, so parse to Guid in memory and drop blank/unparseable references.
        var grouped = await db.Products
            .AsNoTracking()
            .GroupBy(p => p.SupplierId)
            .Select(g => new { SupplierId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, int>();
        foreach (var row in grouped)
            if (Guid.TryParse(row.SupplierId, out Guid supplierId))
                result[supplierId] = row.Count;

        return result;
    }

    public async Task<IReadOnlyList<ProductExportRow>> GetExportProductsAsync(
        CancellationToken cancellationToken = default)
    {
        List<Product> products = await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return products.Select(ToExportRow).ToList();
    }

    private static ProductExportRow ToExportRow(Product product) => new(
        product.Id,
        product.Name,
        product.Category,
        FormatAttributes(product.Attributes),
        product.Barcode,
        product.PurchasePrice,
        ProductExpenses.Total(product.Expenses),
        product.RealCostPerUnit,
        product.SalePrice,
        product.Quantity,
        product.MinStock,
        product.Location,
        product.SupplierId);

    private static string FormatAttributes(IReadOnlyList<ProductAttribute> attributes) =>
        string.Join("; ", attributes
            .Where(a => !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.Value))
            .Select(a => $"{a.Name.Trim()}: {a.Value.Trim()}"));
}
