using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.DeleteSale;

/// <summary>
/// Deletes a sale, unwinding its business chain in one transaction — the reverse of <c>CreateSale</c>:
/// ① a catalogued sale returns its reserved stock (Products contract) ② a credit sale reduces the
/// customer's debt, flooring at zero if it was already paid down (Customers contract) ③ the sale row is
/// removed ④ the delete is logged. Guarded by the closed-day rule: a sale whose day is already closed
/// cannot be deleted. Stock/debt reversals are best-effort — if the product or customer has since been
/// deleted there is nothing to unwind, and the sale must still be deletable.
/// </summary>
public sealed class DeleteSaleHandler(
    ISalesDbContext db,
    IUnitOfWork unitOfWork,
    IProductsModule products,
    ICustomersModule customers,
    IDayEndModule dayEnd,
    IActivityLogger activityLogger,
    ICurrentUser currentUser,
    IDateProvider dateProvider)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        Sale? sale = await db.Sales.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sale is null)
            return Result.Failure(SaleErrors.NotFound);

        if (await dayEnd.ClosingExistsAsync(dateProvider.ToLocalDate(sale.Date), ct))
            return Result.Failure(SaleErrors.DayClosedConflict);

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        // Reverse the sale's effects (best-effort — the only possible failure is a since-deleted counterparty).
        if (sale.ProductId is { } productId)
            await products.IncreaseStockAsync(productId, sale.Quantity, ct);
        if (sale.PaymentType == PaymentType.Credit && sale.CustomerId is { } customerId)
            await customers.DecreaseDebtAsync(customerId, sale.TotalAmount, ct);

        db.Sales.Remove(sale);

        await activityLogger.LogAsync(
            "Satış sildi",
            $"{sale.ProductName} × {sale.Quantity} — {sale.TotalAmount:0.00} AZN",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success();
    }
}
