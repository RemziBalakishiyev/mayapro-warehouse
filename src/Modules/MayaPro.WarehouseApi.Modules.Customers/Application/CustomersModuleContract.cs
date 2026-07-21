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
internal sealed class CustomersModuleContract(ICustomersDbContext db, IDateProvider dateProvider) : ICustomersModule
{
    public async Task<Result> IncreaseDebtAsync(Guid customerId, decimal amount, CancellationToken cancellationToken = default)
    {
        Customer? customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null)
            return Result.Failure(CustomerErrors.NotFound);

        customer.IncreaseDebt(amount);
        return Result.Success();
    }

    public async Task<Result> DecreaseDebtAsync(Guid customerId, decimal amount, CancellationToken cancellationToken = default)
    {
        Customer? customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null)
            return Result.Failure(CustomerErrors.NotFound);

        customer.ReverseDebt(amount);
        return Result.Success();
    }

    public async Task<decimal> GetTotalDebtAsync(CancellationToken cancellationToken = default) =>
        await db.Customers.AsNoTracking().SumAsync(c => c.Debt, cancellationToken);

    public async Task<IReadOnlyList<RecentPaymentInfo>> GetRecentPaymentsAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.CustomerPayments
            .AsNoTracking()
            .OrderByDescending(p => p.Date)
            .Take(take)
            .Join(
                db.Customers.AsNoTracking(),
                payment => payment.CustomerId,
                customer => customer.Id,
                (payment, customer) => new { payment.Id, payment.Date, CustomerName = customer.Name, payment.Amount })
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(r => r.Date)   // by the raw timestamp — the join may not preserve order
            .Select(r => new RecentPaymentInfo(r.Id, dateProvider.ToLocalDate(r.Date), r.CustomerName, r.Amount))
            .ToList();
    }

    public async Task<Dictionary<Guid, string>> GetNamesAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        List<Guid> idSet = ids.Distinct().ToList();
        if (idSet.Count == 0)
            return new Dictionary<Guid, string>();

        return await db.Customers
            .AsNoTracking()
            .Where(c => idSet.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
    }
}
