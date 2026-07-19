using MayaPro.WarehouseApi.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;

/// <summary>The Customers module's data surface. Handlers depend on this, not on the concrete DbContext.</summary>
public interface ICustomersDbContext
{
    DbSet<Customer> Customers { get; }

    DbSet<CustomerPayment> CustomerPayments { get; }

    DbSet<CustomerDebtAdjustment> CustomerDebtAdjustments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
