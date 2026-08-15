using System.Data.Common;
using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure;

/// <summary>
/// The Auth module's DbContext. Owns the <c>identity</c> schema and nothing else — no other module's
/// tables are visible here. Participates in cross-module transactions via
/// <see cref="ITransactionalDbContext"/> (BE#28), so a salary entry and its activity log commit together.
/// <para>
/// BE#35: users and salary entries are tenant-scoped, so <c>OnModelCreating</c> installs the shared global
/// query filter. The one documented exception is login, which is anonymous by nature and therefore resolves
/// the user with <c>IgnoreQueryFilters()</c> — see <c>LoginHandler</c>.
/// <paramref name="currentTenant"/> is optional purely so unit tests can new the context up with options
/// alone; in the host it is always injected.
/// </para>
/// </summary>
public sealed class AuthDbContext(
    DbContextOptions<AuthDbContext> options,
    ICurrentTenant? currentTenant = null)
    : DbContext(options), IAuthDbContext, ITransactionalDbContext, ITenantAwareDbContext
{
    public const string Schema = "identity";

    public Guid CurrentTenantId => currentTenant?.TenantId ?? Guid.Empty;

    public DbSet<User> Users => Set<User>();

    public DbSet<SalaryEntry> SalaryEntries => Set<SalaryEntry>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
        modelBuilder.ApplyTenantIsolation(this);
    }
}
