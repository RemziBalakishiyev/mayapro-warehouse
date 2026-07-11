using FluentValidation;
using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.AdjustStock;

/// <summary>
/// Applies a manual stock correction (delta + optional note) and records an activity entry — the
/// backend counterpart of the frontend <c>productHandlers.adjustStock</c>.
/// </summary>
public sealed class AdjustStockHandler(
    IProductsDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<AdjustStockCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<ProductDto>> Handle(AdjustStockCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<ProductDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        Product? product = await db.Products.FirstOrDefaultAsync(p => p.Id == command.Id, ct);
        if (product is null)
            return Result.Failure<ProductDto>(ProductErrors.NotFound);

        product.AdjustStock(command.Delta);

        // Transaction so the stock change and its activity log commit together.
        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        string suffix = string.IsNullOrWhiteSpace(command.Note) ? string.Empty : $" ({command.Note})";
        string sign = command.Delta > 0 ? "+" : string.Empty;
        await activityLogger.LogAsync(
            "Stok dəyişdi",
            $"{product.Name} {sign}{command.Delta}{suffix}",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(product.ToDto());
    }
}
