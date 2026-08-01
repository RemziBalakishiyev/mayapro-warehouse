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
internal sealed class ProductsModuleContract(IProductsDbContext db, IDateProvider dateProvider) : IProductsModule
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

        return Result.Success(new ProductStockSnapshot(
            product.Name,
            product.Category,
            product.RealCostPerUnit,
            product.PurchasePrice));
    }

    public async Task<Result> IncreaseStockAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        Product? product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
            return Result.Failure(ProductErrors.NotFound);

        product.IncreaseStock(quantity);
        return Result.Success();
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
        product.SalePrice,
        product.InitialQuantity,
        product.CreatedAt);

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

    public async Task<Result> RemoveExpenseFromProductAsync(
        Guid productId,
        string category,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        Product? product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
            return Result.Failure(ProductErrors.NotFound);

        product.RemoveExpense(category, amount);
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

    public async Task<IReadOnlyList<ProductLabelInfo>> GetLabelInfoAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return Array.Empty<ProductLabelInfo>();

        // Projected in SQL: a label needs four columns, so there is no reason to materialise whole
        // products (their attributes/expenses are nvarchar(max) JSON behind value converters).
        return await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new ProductLabelInfo(p.Id, p.Name, p.Barcode, p.SalePrice))
            .ToListAsync(cancellationToken);
    }

    private static string FormatAttributes(IReadOnlyList<ProductAttribute> attributes) =>
        string.Join("; ", attributes
            .Where(a => !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.Value))
            .Select(a => $"{a.Name.Trim()}: {a.Value.Trim()}"));

    public async Task<IReadOnlyList<StockAdjustmentRow>> GetStockAdjustmentsAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        IQueryable<StockAdjustment> query = db.StockAdjustments.AsNoTracking();

        if (from is { } f)
            query = query.Where(a => a.Date >= dateProvider.LocalDayRangeUtc(f).StartUtc);
        if (to is { } t)
            query = query.Where(a => a.Date < dateProvider.LocalDayRangeUtc(t).EndUtc);

        List<StockAdjustment> adjustments = await query.OrderBy(a => a.Date).ToListAsync(cancellationToken);

        return adjustments
            .Select(a => new StockAdjustmentRow(a.ProductId, a.Delta, dateProvider.ToLocalDate(a.Date)))
            .ToList();
    }
}
