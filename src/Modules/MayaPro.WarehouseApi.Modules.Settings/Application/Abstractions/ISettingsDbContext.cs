using MayaPro.WarehouseApi.Modules.Settings.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Settings.Application.Abstractions;

/// <summary>The Settings module's data surface. Handlers depend on this, not on the concrete DbContext.</summary>
public interface ISettingsDbContext
{
    DbSet<StoreSettings> StoreSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
