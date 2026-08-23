using MayaPro.WarehouseApi.Modules.Auth.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;

/// <summary>The Auth module's data surface. Handlers depend on this, not on the concrete DbContext.</summary>
public interface IAuthDbContext
{
    /// <summary>Login accounts. BE#57: a user is who may sign in, never who is on the payroll.</summary>
    DbSet<User> Users { get; }

    /// <summary>The payroll register — BE#57. Every salary route reads this, not <see cref="Users"/>.</summary>
    DbSet<Employee> Employees { get; }

    /// <summary>Employee salary account lines (payments and deductions) — BE#28.</summary>
    DbSet<SalaryEntry> SalaryEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
