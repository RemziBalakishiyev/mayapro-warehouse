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
    // Two requests can pick the same candidate between the "is it free?" read and the write, so the
    // filtered unique index on Barcode — not the read — is the real guard. Each save attempt that the
    // index rejects gets a fresh candidate.
    private const int MaxSaveAttempts = 5;

    // Probes before falling back to the index: at 10^7 candidates even one collision is already unlikely.
    private const int CandidateProbes = 3;

    public async Task<Result<ProductDto>> Handle(Guid id, CancellationToken ct)
    {
        Product? product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return Result.Failure<ProductDto>(ProductErrors.NotFound);

        if (!string.IsNullOrWhiteSpace(product.Barcode))
            return Result.Failure<ProductDto>(ProductErrors.BarcodeAlreadyExists);

        for (int attempt = 1; ; attempt++)
        {
            product.AssignBarcode(await NextFreeCandidateAsync(ct));

            try
            {
                await db.SaveChangesAsync(ct);
                return Result.Success(product.ToDto());
            }
            catch (DbUpdateException) when (attempt < MaxSaveAttempts)
            {
                // This update touches one column guarded by one unique index, so a rejected save means we
                // lost the race for that exact code: take a new candidate and try again. The last attempt
                // is deliberately left uncaught — a failure that survives every retry is a real fault and
                // must surface instead of being reported as a business error.
            }
        }
    }

    /// <summary>
    /// A candidate the database does not know yet. The check is only a fast path — it cannot rule out a
    /// concurrent writer, which is why the caller still retries on the unique-index violation.
    /// </summary>
    private async Task<string> NextFreeCandidateAsync(CancellationToken ct)
    {
        for (int probe = 0; probe < CandidateProbes; probe++)
        {
            string candidate = BarcodeGenerator.NextCandidate();
            bool taken = await db.Products.AnyAsync(p => p.Barcode == candidate, ct);
            if (!taken)
                return candidate;
        }

        // Every probe hit an existing code — hand the last word to the unique index rather than looping
        // against the database.
        return BarcodeGenerator.NextCandidate();
    }
}
