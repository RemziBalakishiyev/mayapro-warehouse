using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetCustomerHistory;

/// <summary>
/// Returns a customer's complete history in chronological order (oldest first): the opening balance,
/// every sale of ANY payment type (from the Sales module via its contract — only Nisyə ones raised the
/// debt; the paymentType field lets the frontend tell them apart), and every payment. Each source is one
/// query; they are merged and sorted by timestamp in memory.
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

        IReadOnlyList<CustomerSaleEntry> customerSales = await sales.GetSalesByCustomerAsync(customerId, ct);

        var entries = new List<CustomerHistoryEntryDto>(adjustments.Count + payments.Count + customerSales.Count);

        entries.AddRange(adjustments.Select(a => new CustomerHistoryEntryDto(
            a.Date, CustomerHistoryEntryType.InitialDebt, a.Amount, a.Note, SaleId: null, PaymentType: null)));

        entries.AddRange(customerSales.Select(s => new CustomerHistoryEntryDto(
            s.Date, CustomerHistoryEntryType.Sale, s.TotalAmount, $"{s.ProductName} × {s.Quantity}", s.Id, s.PaymentType)));

        entries.AddRange(payments.Select(p => new CustomerHistoryEntryDto(
            p.Date, CustomerHistoryEntryType.Payment, p.Amount, p.Note, SaleId: null, PaymentType: null)));

        return entries.OrderBy(e => e.Date).ToList();
    }
}
