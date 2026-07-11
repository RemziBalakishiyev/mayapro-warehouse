using FluentValidation;
using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateSale;

/// <summary>
/// The sales chain — the heart of the system — in one transaction across modules:
/// ① validate ② begin transaction ③ decrease stock (Products contract) ④ if credit, increase the
/// customer's debt by the net amount (Customers contract) ⑤ write the sale with a cost snapshot
/// ⑥ log the activity ⑦ save every context and commit. Any failure returns before the commit, so the
/// shared transaction rolls back and stock/debt stay untouched.
/// </summary>
public sealed class CreateSaleHandler(
    ISalesDbContext db,
    IUnitOfWork unitOfWork,
    IProductsModule products,
    ICustomersModule customers,
    IValidator<CreateSaleCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<SaleDto>> Handle(CreateSaleCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<SaleDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        // Validated above, so this always succeeds.
        PaymentTypeCode.TryParse(command.PaymentType, out PaymentType paymentType);

        decimal net = command.SalePrice * command.Quantity - command.Discount;

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        // ③ Reserve stock (cost snapshot comes back for the sale record).
        Result<ProductStockSnapshot> stock =
            await products.TryDecreaseStockAsync(command.ProductId, command.Quantity, ct);
        if (stock.IsFailure)
            return Result.Failure<SaleDto>(stock.Error);

        // ④ Credit sale → increase the customer's debt by the net (post-discount) amount.
        if (paymentType == PaymentType.Credit)
        {
            Result debt = await customers.IncreaseDebtAsync(command.CustomerId!.Value, net, ct);
            if (debt.IsFailure)
                return Result.Failure<SaleDto>(debt.Error);
        }

        // ⑤ Record the sale with the cost snapshot.
        var sale = Sale.Create(
            command.ProductId,
            stock.Value.ProductName,
            command.Quantity,
            command.SalePrice,
            command.Discount,
            stock.Value.RealCostPerUnit,
            paymentType,
            command.CustomerId,
            currentUser.UserId,
            currentUser.Name ?? string.Empty);

        db.Sales.Add(sale);

        // ⑥ Activity log (note the discount if any).
        string discountNote = command.Discount > 0 ? $" (endirim {command.Discount:0.00})" : string.Empty;
        await activityLogger.LogAsync(
            "Satış etdi",
            $"{sale.ProductName} × {sale.Quantity} — {sale.TotalAmount:0.00} AZN{discountNote}",
            currentUser.UserId,
            ct);

        // ⑦ Persist every enlisted context, then commit atomically.
        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(sale.ToDto());
    }
}
