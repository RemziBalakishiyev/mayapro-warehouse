using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetCustomers;

/// <summary>
/// Returns every customer, newest first, enriched with paid-amount and last-payment (from this module's
/// payments) and last-purchase (last credit sale, via the Sales contract). Both aggregates are one
/// grouped query each — never a query per customer.
/// </summary>
public sealed class GetCustomersHandler(ICustomersDbContext db, ISalesModule sales)
{
    public async Task<IReadOnlyList<CustomerDto>> Handle(CancellationToken ct)
    {
        List<Customer> customers = await db.Customers
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        // One grouped query for all customers' payment stats (paid total + last payment).
        Dictionary<Guid, (decimal Paid, DateTime Last)> paymentStats = await db.CustomerPayments
            .AsNoTracking()
            .GroupBy(p => p.CustomerId)
            .Select(g => new { CustomerId = g.Key, Paid = g.Sum(p => p.Amount), Last = g.Max(p => p.Date) })
            .ToDictionaryAsync(x => x.CustomerId, x => (x.Paid, x.Last), ct);

        // One grouped query for the opening balances each customer was migrated in with.
        Dictionary<Guid, decimal> initialDebts = await db.CustomerDebtAdjustments
            .AsNoTracking()
            .GroupBy(a => a.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total, ct);

        // One cross-module query for the last credit-sale date per customer.
        Dictionary<Guid, DateTime> lastPurchase = (await sales.GetLastCreditSaleDatesByCustomerAsync(ct))
            .ToDictionary(x => x.CustomerId, x => x.Date);

        return customers
            .Select(c =>
            {
                (decimal paid, DateTime? lastPayment) = paymentStats.TryGetValue(c.Id, out var stat)
                    ? (stat.Paid, stat.Last)
                    : (0m, (DateTime?)null);
                DateTime? lastPurchaseDate = lastPurchase.TryGetValue(c.Id, out DateTime lp) ? lp : null;
                decimal initialDebt = initialDebts.TryGetValue(c.Id, out decimal init) ? init : 0m;
                return c.ToDto(initialDebt, paid, lastPurchaseDate, lastPayment);
            })
            .ToList();
    }
}
