using MayaPro.WarehouseApi.Modules.Auth.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;

/// <summary>The Auth module's data surface. Handlers depend on this, not on the concrete DbContext.</summary>
public interface IAuthDbContext
{
    DbSet<User> Users { get; }

    /// <summary>Employee salary account lines (payments and deductions) — BE#28.</summary>
    DbSet<SalaryEntry> SalaryEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
