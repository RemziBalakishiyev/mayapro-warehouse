using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetCustomers;

/// <summary>Returns every customer, newest first.</summary>
public sealed class GetCustomersHandler(ICustomersDbContext db)
{
    public async Task<IReadOnlyList<CustomerDto>> Handle(CancellationToken ct)
    {
        List<Customer> customers = await db.Customers
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return customers.Select(c => c.ToDto()).ToList();
    }
}
