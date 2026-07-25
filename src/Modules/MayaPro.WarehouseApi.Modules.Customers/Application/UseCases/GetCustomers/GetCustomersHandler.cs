using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetCustomers;

/// <summary>
/// Returns every customer, newest first, enriched with paid-amount and last-payment (from this module's
/// payments) and purchase stats over ALL sale types — total, count, last purchase (via the Sales
/// contract). Every aggregate is one grouped query — never a query per customer.
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

        // One cross-module grouped query for each customer's purchase stats over every sale type.
        Dictionary<Guid, CustomerPurchaseStats> purchaseStats = (await sales.GetPurchaseStatsByCustomerAsync(ct))
            .ToDictionary(x => x.CustomerId);

        return customers
            .Select(c =>
            {
                (decimal paid, DateTime? lastPayment) = paymentStats.TryGetValue(c.Id, out var stat)
                    ? (stat.Paid, stat.Last)
                    : (0m, (DateTime?)null);
                CustomerPurchaseStats? purchases = purchaseStats.GetValueOrDefault(c.Id);
                decimal initialDebt = initialDebts.TryGetValue(c.Id, out decimal init) ? init : 0m;
                return c.ToDto(
                    initialDebt,
                    paid,
                    purchases?.TotalPurchases ?? 0m,
                    purchases?.PurchaseCount ?? 0,
                    purchases?.LastPurchase,
                    lastPayment);
            })
            .ToList();
    }
}
