using FluentValidation;
using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.UpdateProduct;

/// <summary>Edits an existing product, recomputing its real cost, and records an activity entry.</summary>
public sealed class UpdateProductHandler(
    IProductsDbContext db,
    IValidator<UpdateProductCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<ProductDto>> Handle(UpdateProductCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<ProductDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        Product? product = await db.Products.FirstOrDefaultAsync(p => p.Id == command.Id, ct);
        if (product is null)
            return Result.Failure<ProductDto>(ProductErrors.NotFound);

        var expenses = new ProductExpenses(
            command.Expenses.Yol,
            command.Expenses.Fehle,
            command.Expenses.Yer,
            command.Expenses.Paket,
            command.Expenses.Diger);

        product.Update(
            command.Name,
            command.Category,
            command.Size,
            command.Color,
            command.Model,
            command.Barcode,
            command.Image,
            command.Note,
            command.PurchasePrice,
            command.SalePrice,
            command.Quantity,
            command.MinStock,
            command.Currency,
            command.SupplierId,
            command.Location,
            command.Store,
            command.Warehouse,
            command.Shelf,
            command.Box,
            expenses);

        await db.SaveChangesAsync(ct);

        await activityLogger.LogAsync(
            "Mal redaktə etdi",
            product.Name,
            currentUser.UserId,
            ct);

        return Result.Success(product.ToDto());
    }
}
