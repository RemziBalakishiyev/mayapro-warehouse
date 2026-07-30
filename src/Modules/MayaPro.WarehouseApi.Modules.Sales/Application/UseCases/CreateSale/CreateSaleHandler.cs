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
/// customer's debt by the total amount (Customers contract) ⑤ write the sale with a cost snapshot
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
        PaymentTypeCode.TryParse(command.PaymentType, out PaymentType requestedType);

        decimal total = command.SalePrice * command.Quantity;

        // BE#15 — qismən ödənişli satış: resolves what actually gets stored (paid amount, remaining balance,
        // final payment type and paid-via method) from what was requested.
        SalePaymentPlan plan = SalePaymentPlan.Resolve(requestedType, total, command.PaidAmount, command.PaidVia);
        PaymentType paymentType = plan.PaymentType;

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        // ③ Build the sale. A normal sale reserves stock and snapshots the product's real cost; a free-form
        // (manual) sale has no product, so the stock step is skipped and the seller-supplied name/cost are used.
        Sale sale;
        if (command.ProductId is { } productId)
        {
            Result<ProductStockSnapshot> stock =
                await products.TryDecreaseStockAsync(productId, command.Quantity, ct);
            if (stock.IsFailure)
                return Result.Failure<SaleDto>(stock.Error);

            // ④ A remaining balance → increase the customer's debt by only that remaining amount, not the total.
            if (paymentType == PaymentType.Credit)
            {
                Result debt = await customers.IncreaseDebtAsync(command.CustomerId!.Value, plan.Remaining, ct);
                if (debt.IsFailure)
                    return Result.Failure<SaleDto>(debt.Error);
            }

            sale = Sale.Create(
                productId,
                stock.Value.ProductName,
                stock.Value.Category,
                command.Quantity,
                command.SalePrice,
                stock.Value.RealCostPerUnit,
                stock.Value.PurchasePrice,
                paymentType,
                command.CustomerId,
                currentUser.UserId,
                currentUser.Name ?? string.Empty,
                plan.PaidAmount,
                plan.PaidVia);
        }
        else
        {
            // ④ Credit still increases the customer's debt (only the remaining amount) — the money is just as
            // real as a catalogued sale.
            if (paymentType == PaymentType.Credit)
            {
                Result debt = await customers.IncreaseDebtAsync(command.CustomerId!.Value, plan.Remaining, ct);
                if (debt.IsFailure)
                    return Result.Failure<SaleDto>(debt.Error);
            }

            // Expense lines are documentation only — stored verbatim, never used to recompute the cost.
            IReadOnlyList<SaleExpenseItem> expenseItems = command.ExpenseItems is { Count: > 0 } items
                ? items.Select(e => new SaleExpenseItem(e.Name, e.Amount)).ToList()
                : Array.Empty<SaleExpenseItem>();

            sale = Sale.CreateManual(
                command.ProductName!,
                command.Category,
                command.Quantity,
                command.SalePrice,
                command.CostPerUnit,
                paymentType,
                command.CustomerId,
                currentUser.UserId,
                currentUser.Name ?? string.Empty,
                expenseItems,
                command.PurchasePricePerUnit,
                plan.PaidAmount,
                plan.PaidVia);
        }

        // ⑤ Record the sale.
        db.Sales.Add(sale);

        // ⑥ Activity log (flag free-form sales).
        string manualNote = sale.IsManual ? " (sərbəst satış)" : string.Empty;
        await activityLogger.LogAsync(
            "Satış etdi",
            $"{sale.ProductName} × {sale.Quantity} — {sale.TotalAmount:0.00} AZN{manualNote}",
            currentUser.UserId,
            ct);

        // ⑦ Persist every enlisted context, then commit atomically.
        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(sale.ToDto());
    }
}
