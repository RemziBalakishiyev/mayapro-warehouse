using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.GenerateBarcode;

/// <summary>
/// Assigns a unique, system-generated barcode (<c>"SDK" + 7 digits</c>) to a product that does not have
/// one yet. A product that already carries a barcode is rejected with
/// <see cref="ProductErrors.BarcodeAlreadyExists"/> — re-generation is not allowed, matching the "print
/// once" workflow for physical labels.
/// </summary>
public sealed class GenerateBarcodeHandler(IProductsDbContext db)
{
    // The unique index makes a real collision astronomically unlikely (10^7 candidates); a handful of
    // retries is just a safety net, not a load-bearing loop.
    private const int MaxAttempts = 20;

    public async Task<Result<ProductDto>> Handle(Guid id, CancellationToken ct)
    {
        Product? product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return Result.Failure<ProductDto>(ProductErrors.NotFound);

        if (!string.IsNullOrWhiteSpace(product.Barcode))
            return Result.Failure<ProductDto>(ProductErrors.BarcodeAlreadyExists);

        string barcode = await NextUniqueBarcodeAsync(ct);
        product.AssignBarcode(barcode);

        await db.SaveChangesAsync(ct);

        return Result.Success(product.ToDto());
    }

    private async Task<string> NextUniqueBarcodeAsync(CancellationToken ct)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            string candidate = BarcodeGenerator.NextCandidate();
            bool taken = await db.Products.AnyAsync(p => p.Barcode == candidate, ct);
            if (!taken)
                return candidate;
        }

        // Only reachable if MaxAttempts collisions happen in a row — effectively impossible at 10^7
        // candidates, but fail loudly rather than silently assign a duplicate.
        throw new InvalidOperationException("Unikal barkod generasiya edilə bilmədi, yenidən cəhd edin.");
    }
}
