using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetCustomerHistory;

/// <summary>
/// Returns a customer's complete debt history in chronological order (oldest first): the opening balance,
/// every credit sale (from the Sales module via its contract), and every payment. Each source is one query;
/// they are merged and sorted by timestamp in memory.
/// </summary>
public sealed class GetCustomerHistoryHandler(ICustomersDbContext db, ISalesModule sales)
{
    public async Task<IReadOnlyList<CustomerHistoryEntryDto>> Handle(Guid customerId, CancellationToken ct)
    {
        List<CustomerDebtAdjustment> adjustments = await db.CustomerDebtAdjustments
            .AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .ToListAsync(ct);

        List<CustomerPayment> payments = await db.CustomerPayments
            .AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .ToListAsync(ct);

        IReadOnlyList<CustomerCreditSale> creditSales = await sales.GetCreditSalesByCustomerAsync(customerId, ct);

        var entries = new List<CustomerHistoryEntryDto>(adjustments.Count + payments.Count + creditSales.Count);

        entries.AddRange(adjustments.Select(a => new CustomerHistoryEntryDto(
            a.Date, CustomerHistoryEntryType.InitialDebt, a.Amount, a.Note)));

        entries.AddRange(creditSales.Select(s => new CustomerHistoryEntryDto(
            s.Date, CustomerHistoryEntryType.Sale, s.TotalAmount, $"{s.ProductName} × {s.Quantity}")));

        entries.AddRange(payments.Select(p => new CustomerHistoryEntryDto(
            p.Date, CustomerHistoryEntryType.Payment, p.Amount, p.Note)));

        return entries.OrderBy(e => e.Date).ToList();
    }
}
