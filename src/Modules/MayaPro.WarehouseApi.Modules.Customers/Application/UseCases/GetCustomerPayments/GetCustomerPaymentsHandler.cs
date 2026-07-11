using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetCustomerPayments;

/// <summary>Returns a customer's payments, newest first.</summary>
public sealed class GetCustomerPaymentsHandler(ICustomersDbContext db)
{
    public async Task<IReadOnlyList<CustomerPaymentDto>> Handle(Guid customerId, CancellationToken ct)
    {
        List<CustomerPayment> payments = await db.CustomerPayments
            .AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.Date)
            .ToListAsync(ct);

        return payments.Select(p => p.ToDto()).ToList();
    }
}
