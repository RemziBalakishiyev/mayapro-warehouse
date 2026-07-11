using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;

/// <summary>The Suppliers module's data surface. Handlers depend on this, not on the concrete DbContext.</summary>
public interface ISuppliersDbContext
{
    DbSet<Supplier> Suppliers { get; }

    DbSet<SupplierPayment> SupplierPayments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
