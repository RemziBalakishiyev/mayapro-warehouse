using MayaPro.WarehouseApi.Modules.Auth.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;

/// <summary>The Auth module's data surface. Handlers depend on this, not on the concrete DbContext.</summary>
public interface IAuthDbContext
{
    DbSet<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
