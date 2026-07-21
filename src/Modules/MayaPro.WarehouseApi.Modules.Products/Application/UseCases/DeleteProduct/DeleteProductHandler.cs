using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.DeleteProduct;

/// <summary>
/// Deletes a product and logs it, in one transaction. Deletion is always allowed: past sales carry their own
/// name/cost snapshots, so removing the catalogue item never distorts history. Remaining stock is only a
/// frontend warning — the backend does not block. Sales, expenses and suppliers keep their loose product-id
/// references (no cross-module FK); a since-deleted product simply resolves to no current name.
/// </summary>
public sealed class DeleteProductHandler(
    IProductsDbContext db,
    IUnitOfWork unitOfWork,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        Product? product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return Result.Failure(ProductErrors.NotFound);

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        db.Products.Remove(product);

        await activityLogger.LogAsync("Mal sildi", product.Name, currentUser.UserId, ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success();
    }
}
