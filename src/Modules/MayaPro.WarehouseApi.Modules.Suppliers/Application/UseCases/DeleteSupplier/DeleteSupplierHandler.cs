using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.DeleteSupplier;

/// <summary>
/// Deletes a supplier along with their payment history, in one transaction. A supplier we still owe money
/// cannot be deleted (→ 409). Products keep their loose <c>SupplierId</c> reference — no cross-module FK.
/// </summary>
public sealed class DeleteSupplierHandler(
    ISuppliersDbContext db,
    IUnitOfWork unitOfWork,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        Supplier? supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null)
            return Result.Failure(SupplierErrors.NotFound);

        if (supplier.Debt > 0m)
            return Result.Failure(SupplierErrors.HasDebtConflict);

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        List<SupplierPayment> payments = await db.SupplierPayments
            .Where(p => p.SupplierId == id)
            .ToListAsync(ct);
        db.SupplierPayments.RemoveRange(payments);

        db.Suppliers.Remove(supplier);

        await activityLogger.LogAsync("Təchizatçı sildi", supplier.Name, currentUser.UserId, ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success();
    }
}
