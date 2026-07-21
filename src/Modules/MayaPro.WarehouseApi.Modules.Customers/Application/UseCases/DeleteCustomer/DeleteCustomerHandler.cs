using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.DeleteCustomer;

/// <summary>
/// Deletes a customer along with their payment and opening-balance history, in one transaction. A customer
/// who still owes money cannot be deleted (→ 409). Past sales keep their <c>CustomerId</c> — the name lookup
/// simply returns nothing, which the frontend renders as "Silinmiş müştəri".
/// </summary>
public sealed class DeleteCustomerHandler(
    ICustomersDbContext db,
    IUnitOfWork unitOfWork,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        Customer? customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
            return Result.Failure(CustomerErrors.NotFound);

        if (customer.Debt > 0m)
            return Result.Failure(CustomerErrors.HasDebtConflict);

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        // Remove the customer's history first, then the customer itself.
        List<CustomerPayment> payments = await db.CustomerPayments
            .Where(p => p.CustomerId == id)
            .ToListAsync(ct);
        db.CustomerPayments.RemoveRange(payments);

        List<CustomerDebtAdjustment> adjustments = await db.CustomerDebtAdjustments
            .Where(a => a.CustomerId == id)
            .ToListAsync(ct);
        db.CustomerDebtAdjustments.RemoveRange(adjustments);

        db.Customers.Remove(customer);

        await activityLogger.LogAsync("Müştəri sildi", customer.Name, currentUser.UserId, ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success();
    }
}
