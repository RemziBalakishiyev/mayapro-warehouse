using FluentValidation;
using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CreateProduct;

/// <summary>
/// Creates a product: computes the real cost (via the domain), persists it, and records an activity
/// entry — the backend counterpart of the frontend <c>productHandlers.create</c>.
/// </summary>
public sealed class CreateProductHandler(
    IProductsDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<CreateProductCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<ProductDto>> Handle(CreateProductCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<ProductDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        var expenses = new ProductExpenses(
            command.Expenses.Yol,
            command.Expenses.Fehle,
            command.Expenses.Yer,
            command.Expenses.Paket,
            command.Expenses.Diger);

        var product = Product.Create(
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

        // Transaction so the product insert and its activity log commit together.
        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        db.Products.Add(product);

        await activityLogger.LogAsync(
            "Mal əlavə etdi",
            $"{product.Name} — {product.Quantity} ədəd",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(product.ToDto());
    }
}
