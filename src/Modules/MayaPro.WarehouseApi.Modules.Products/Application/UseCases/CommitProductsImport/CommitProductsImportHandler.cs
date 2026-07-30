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
/// <para>
/// The token is claimed (removed) up front, so a replay or two parallel commits cannot apply the same import
/// twice; if the transaction does not go through, the entry is put back and the user can retry without
/// re-uploading.
/// </para>
/// </summary>
public sealed class CommitProductsImportHandler(
    IProductsDbContext db,
    IUnitOfWork unitOfWork,
    IImportTokenCache cache,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    /// <summary>The template has no currency column; the store's own currency is what every product uses.</summary>
    private const string DefaultCurrency = "AZN";

    public async Task<Result> Handle(CommitProductsImportCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.ImportToken))
            return Result.Failure(ImportErrors.TokenNotFound);

        string token = command.ImportToken;
        (ImportTokenState state, CachedImportResult? cached) = cache.Claim(token);

        if (state == ImportTokenState.Expired)
            return Result.Failure(ImportErrors.TokenExpired);
        if (state != ImportTokenState.Found || cached is null)
            return Result.Failure(ImportErrors.TokenNotFound);

        // A preview belongs to the user who ran it: another account may not commit someone else's file.
        // Reported as "not found" so the endpoint never confirms that a foreign token exists.
        if (cached.OwnerUserId != currentUser.UserId)
        {
            cache.Restore(token, cached);
            return Result.Failure(ImportErrors.TokenNotFound);
        }

        try
        {
            Result result = await ApplyAsync(cached, ct);
            if (result.IsFailure)
                cache.Restore(token, cached);

            return result;
        }
        catch
        {
            // The transaction rolled back — nothing was applied, so the token stays usable for a retry.
            cache.Restore(token, cached);
            throw;
        }
    }

    private async Task<Result> ApplyAsync(CachedImportResult cached, CancellationToken ct)
    {
        List<CachedImportRow> validRows = cached.Rows
            .Where(r => r.Status != ImportRowStatus.Error && r.Data is not null)
            .ToList();

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        await AddNewCategoriesAsync(cached.NewCategories, ct);

        // Both lookups are single batched queries: a 1000-row file must not become 1000 round trips.
        Dictionary<Guid, Product> productsToUpdate = await LoadUpdateTargetsAsync(validRows, ct);
        if (await BarcodeTakenSincePreviewAsync(validRows, ct))
            return Result.Failure(ImportErrors.BarcodeConflict);

        int created = 0;
        int updated = 0;

        foreach (CachedImportRow row in validRows)
        {
            ImportRowData data = row.Data!;

            if (row.Status == ImportRowStatus.Update)
            {
                // Deleted between preview and commit — skip that row rather than fail the whole batch.
                if (row.ExistingProductId is not { } id || !productsToUpdate.TryGetValue(id, out Product? product))
                    continue;

                ApplyUpdate(product, data);
                updated++;
            }
            else
            {
                db.Products.Add(BuildProduct(data));
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

        return Result.Success();
    }

    /// <summary>Creates the categories the preview flagged, skipping any that appeared in the meantime.</summary>
    private async Task AddNewCategoriesAsync(IReadOnlyList<string> newCategories, CancellationToken ct)
    {
        if (newCategories.Count == 0)
            return;

        List<string> existingNames = await db.Categories.Select(c => c.Name).ToListAsync(ct);
        var existing = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        foreach (string name in newCategories)
        {
            if (existing.Add(name))
                db.Categories.Add(Category.Create(name));
        }
    }

    /// <summary>Loads every product an <c>update</c> row targets in one query, keyed by id.</summary>
    private async Task<Dictionary<Guid, Product>> LoadUpdateTargetsAsync(
        IReadOnlyList<CachedImportRow> validRows,
        CancellationToken ct)
    {
        List<Guid> ids = validRows
            .Where(r => r.Status == ImportRowStatus.Update && r.ExistingProductId is not null)
            .Select(r => r.ExistingProductId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return [];

        return await db.Products
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);
    }

    /// <summary>
    /// True when a barcode this import would insert as new already belongs to a product — someone created it
    /// between the preview and the commit. Detected up front so the whole import is refused with a clear
    /// message instead of the insert failing on the unique index.
    /// </summary>
    private async Task<bool> BarcodeTakenSincePreviewAsync(
        IReadOnlyList<CachedImportRow> validRows,
        CancellationToken ct)
    {
        List<string> newBarcodes = validRows
            .Where(r => r.Status == ImportRowStatus.Create && !string.IsNullOrWhiteSpace(r.Data!.Barcode))
            .Select(r => r.Data!.Barcode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return newBarcodes.Count > 0 &&
               await db.Products.AnyAsync(p => newBarcodes.Contains(p.Barcode), ct);
    }

    private static void ApplyUpdate(Product product, ImportRowData data) =>
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

    private static Product BuildProduct(ImportRowData data) =>
        Product.Create(
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
            currency: DefaultCurrency,
            supplierId: string.Empty,
            BuildLocation(data),
            data.Store,
            data.Warehouse,
            data.Shelf,
            data.Box,
            expenses: ProductExpenses.Empty);

    private static List<ProductAttribute> ToAttributes(IReadOnlyList<ProductAttributeDto> attributes) =>
        attributes.Select(a => new ProductAttribute(a.Name, a.Value)).ToList();

    /// <summary>
    /// Builds the compact display address from the separate location fields, same shape as the seed data.
    /// Capped at the column's length so four long location cells cannot overflow it.
    /// </summary>
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

        string location = string.Join(" / ", parts);
        return location.Length <= ImportTemplate.MaxLocationLength
            ? location
            : location[..ImportTemplate.MaxLocationLength];
    }
}
