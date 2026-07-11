using MayaPro.WarehouseApi.Modules.Sales.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;

/// <summary>The Sales module's data surface. Handlers depend on this, not on the concrete DbContext.</summary>
public interface ISalesDbContext
{
    DbSet<Sale> Sales { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
