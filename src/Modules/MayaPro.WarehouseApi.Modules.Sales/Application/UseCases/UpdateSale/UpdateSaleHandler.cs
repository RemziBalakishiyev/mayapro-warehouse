using FluentValidation;
using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.UpdateSale;

/// <summary>
/// Revises a sale by reverse-and-reapply, all in one transaction: the old sale's stock and debt effects are
/// unwound (best-effort), then the <c>CreateSale</c> chain is applied afresh with the new values on the same
/// row — its <see cref="Sale.Id"/>, date and seller are preserved. Reapplying decrements stock for the new
/// quantity/product, so a shortfall fails the whole update and rolls back (leaving stock untouched). Guarded
/// by the closed-day rule: a sale whose day is already closed cannot be edited.
/// </summary>
public sealed class UpdateSaleHandler(
    ISalesDbContext db,
    IUnitOfWork unitOfWork,
    IProductsModule products,
    ICustomersModule customers,
    IDayEndModule dayEnd,
    IValidator<UpdateSaleCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser,
    IDateProvider dateProvider)
{
    public async Task<Result<SaleDto>> Handle(UpdateSaleCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<SaleDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        Sale? sale = await db.Sales.FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (sale is null)
            return Result.Failure<SaleDto>(SaleErrors.NotFound);

        if (await dayEnd.ClosingExistsAsync(dateProvider.ToLocalDate(sale.Date), ct))
            return Result.Failure<SaleDto>(SaleErrors.DayClosedConflict);

        // Validated above, so this always succeeds.
        PaymentTypeCode.TryParse(command.PaymentType, out PaymentType paymentType);
        decimal net = command.SalePrice * command.Quantity - command.Discount;

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        // ① Reverse the old effects (best-effort — the only possible failure is a since-deleted counterparty).
        if (sale.ProductId is { } oldProductId)
            await products.IncreaseStockAsync(oldProductId, sale.Quantity, ct);
        if (sale.PaymentType == PaymentType.Credit && sale.CustomerId is { } oldCustomerId)
            await customers.DecreaseDebtAsync(oldCustomerId, sale.TotalAmount, ct);

        // ② Reapply with the new values — same chain as CreateSale; any shortfall rolls the whole thing back.
        if (command.ProductId is { } productId)
        {
            Result<ProductStockSnapshot> stock =
                await products.TryDecreaseStockAsync(productId, command.Quantity, ct);
            if (stock.IsFailure)
                return Result.Failure<SaleDto>(stock.Error);

            if (paymentType == PaymentType.Credit)
            {
                Result debt = await customers.IncreaseDebtAsync(command.CustomerId!.Value, net, ct);
                if (debt.IsFailure)
                    return Result.Failure<SaleDto>(debt.Error);
            }

            sale.ReviseCatalogued(
                productId,
                stock.Value.ProductName,
                stock.Value.Category,
                command.Quantity,
                command.SalePrice,
                command.Discount,
                stock.Value.RealCostPerUnit,
                paymentType,
                command.CustomerId);
        }
        else
        {
            if (paymentType == PaymentType.Credit)
            {
                Result debt = await customers.IncreaseDebtAsync(command.CustomerId!.Value, net, ct);
                if (debt.IsFailure)
                    return Result.Failure<SaleDto>(debt.Error);
            }

            IReadOnlyList<SaleExpenseItem> expenseItems = command.ExpenseItems is { Count: > 0 } items
                ? items.Select(e => new SaleExpenseItem(e.Name, e.Amount)).ToList()
                : Array.Empty<SaleExpenseItem>();

            sale.ReviseManual(
                command.ProductName!,
                command.Category,
                command.Quantity,
                command.SalePrice,
                command.Discount,
                command.CostPerUnit,
                paymentType,
                command.CustomerId,
                expenseItems);
        }

        await activityLogger.LogAsync(
            "Satışı düzəltdi",
            $"{sale.ProductName} × {sale.Quantity} — {sale.TotalAmount:0.00} AZN",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(sale.ToDto());
    }
}
