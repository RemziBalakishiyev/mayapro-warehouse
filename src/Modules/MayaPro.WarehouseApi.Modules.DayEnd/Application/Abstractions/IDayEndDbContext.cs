using MayaPro.WarehouseApi.Modules.DayEnd.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Application.Abstractions;

/// <summary>The DayEnd module's data surface. Handlers depend on this, not on the concrete DbContext.</summary>
public interface IDayEndDbContext
{
    DbSet<Closing> Closings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
