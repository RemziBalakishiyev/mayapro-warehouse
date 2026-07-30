using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Products.Application.Imports;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CommitProductsImport;

/// <summary>
/// Applies a previously-previewed Excel import: creates any categories the preview flagged as new, inserts
/// the <c>create</c> rows as new products (real cost = purchase price, no batch expenses — see
/// <see cref="Product.CalculateRealCost"/>) and applies the <c>update</c> rows to the existing product each
/// matched by barcode. <c>error</c> rows are always skipped. Everything commits in one transaction, with a
/// single aggregate activity entry — same shape as <c>AdjustStockHandler</c>: begin the transaction, log,
/// then save and commit.
/// </summary>
public sealed class CommitProductsImportHandler(
    IProductsDbContext db,
    IUnitOfWork unitOfWork,
    IImportTokenCache cache,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result> Handle(CommitProductsImportCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.ImportToken))
            return Result.Failure(ImportErrors.TokenNotFound);

        (ImportTokenState state, CachedImportResult? cached) = cache.TryGet(command.ImportToken);
        if (state == ImportTokenState.NotFound)
            return Result.Failure(ImportErrors.TokenNotFound);
        if (state != ImportTokenState.Found || cached is null)
            return Result.Failure(ImportErrors.TokenExpired);

        List<CachedImportRow> validRows = cached.Rows
            .Where(r => r.Status != ImportRowStatus.Error && r.Data is not null)
            .ToList();

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        if (cached.NewCategories.Count > 0)
        {
            List<string> existingCategoryNames = await db.Categories.Select(c => c.Name).ToListAsync(ct);
            var existingSet = new HashSet<string>(existingCategoryNames, StringComparer.OrdinalIgnoreCase);
            foreach (string name in cached.NewCategories)
            {
                if (existingSet.Add(name))
                    db.Categories.Add(Category.Create(name));
            }
        }

        int created = 0;
        int updated = 0;

        foreach (CachedImportRow row in validRows)
        {
            ImportRowData data = row.Data!;

            if (row.Status == ImportRowStatus.Update && row.ExistingProductId is { } existingId)
            {
                Product? product = await db.Products.FirstOrDefaultAsync(p => p.Id == existingId, ct);
                if (product is null)
                    continue; // Deleted between preview and commit — skip rather than fail the whole batch.

                product.Update(
                    data.Name,
                    data.Category,
                    ToAttributes(data.Attributes),
                    product.Barcode, // matched by barcode — never changed by the import itself
                    product.Image,
                    data.Note,
                    data.PurchasePrice,
                    data.SalePrice,
                    data.Quantity,
                    data.MinStock,
                    product.Currency,
                    product.SupplierId,
                    BuildLocation(data),
                    data.Store,
                    data.Warehouse,
                    data.Shelf,
                    data.Box,
                    product.Expenses);
                updated++;
            }
            else if (row.Status == ImportRowStatus.Create)
            {
                var product = Product.Create(
                    data.Name,
                    data.Category,
                    ToAttributes(data.Attributes),
                    data.Barcode,
                    image: string.Empty,
                    data.Note,
                    data.PurchasePrice,
                    data.SalePrice,
                    data.Quantity,
                    data.MinStock,
                    currency: "AZN",
                    supplierId: string.Empty,
                    BuildLocation(data),
                    data.Store,
                    data.Warehouse,
                    data.Shelf,
                    data.Box,
                    expenses: Array.Empty<ProductExpenseItem>());
                db.Products.Add(product);
                created++;
            }
        }

        await activityLogger.LogAsync(
            "Excel idxalı",
            $"Excel import: {created} yeni, {updated} yenilənmə",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // One-time token: a second commit with the same value must not replay the import.
        cache.Remove(command.ImportToken);

        return Result.Success();
    }

    private static List<ProductAttribute> ToAttributes(IReadOnlyList<ProductAttributeDto> attributes) =>
        attributes.Select(a => new ProductAttribute(a.Name, a.Value)).ToList();

    /// <summary>Builds the compact display address from the separate location fields, same shape as the seed data.</summary>
    private static string BuildLocation(ImportRowData data)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(data.Warehouse))
            parts.Add(data.Warehouse);
        if (!string.IsNullOrWhiteSpace(data.Store) &&
            !string.Equals(data.Store, data.Warehouse, StringComparison.OrdinalIgnoreCase))
            parts.Add(data.Store);
        if (!string.IsNullOrWhiteSpace(data.Shelf))
            parts.Add($"Rəf {data.Shelf}");
        if (!string.IsNullOrWhiteSpace(data.Box))
            parts.Add($"Qutu {data.Box}");

        return string.Join(" / ", parts);
    }
}
