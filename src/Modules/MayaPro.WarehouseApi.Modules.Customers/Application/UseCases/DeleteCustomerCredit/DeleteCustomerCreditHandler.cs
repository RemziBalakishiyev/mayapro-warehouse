using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.DeleteCustomerCredit;

/// <summary>
/// Removes one nisyə (credit) debt line for a customer — the Nisyə borclar UI entry point. Delegates the
/// stock/debt unwind to the Sales module contract; never deletes the customer themselves.
/// </summary>
public sealed class DeleteCustomerCreditHandler(ICustomersDbContext db, ISalesModule sales)
{
    public async Task<Result> Handle(Guid customerId, Guid saleId, CancellationToken ct)
    {
        Customer? customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null)
            return Result.Failure(CustomerErrors.NotFound);

        Result delete = await sales.DeleteCreditSaleAsync(saleId, customerId, ct);
        if (delete.IsFailure)
            return Result.Failure(CustomerErrors.CreditSaleNotFound);

        return Result.Success();
    }
}
