using MayaPro.WarehouseApi.Modules.Activity.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Activity.Application.Abstractions;

/// <summary>The Activity module's data surface. Handlers/logger depend on this, not the concrete DbContext.</summary>
public interface IActivityDbContext
{
    DbSet<ActivityLog> ActivityLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
