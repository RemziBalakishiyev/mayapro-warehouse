using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Application;

/// <summary>
/// The Customers module's implementation of <see cref="ICustomersModule"/>. Loads the customer tracked so
/// the debt increase is part of the caller's unit of work; does not save.
/// </summary>
internal sealed class CustomersModuleContract(ICustomersDbContext db) : ICustomersModule
{
    public async Task<Result> IncreaseDebtAsync(Guid customerId, decimal amount, CancellationToken cancellationToken = default)
    {
        Customer? customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null)
            return Result.Failure(CustomerErrors.NotFound);

        customer.IncreaseDebt(amount);
        return Result.Success();
    }

    public async Task<decimal> GetTotalDebtAsync(CancellationToken cancellationToken = default) =>
        await db.Customers.AsNoTracking().SumAsync(c => c.Debt, cancellationToken);
}
