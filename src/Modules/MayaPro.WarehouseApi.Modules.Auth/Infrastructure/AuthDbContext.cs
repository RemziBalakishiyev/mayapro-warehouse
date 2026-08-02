using System.Data.Common;
using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure;

/// <summary>
/// The Auth module's DbContext. Owns the <c>identity</c> schema and nothing else — no other module's
/// tables are visible here. Participates in cross-module transactions via
/// <see cref="ITransactionalDbContext"/> (BE#28), so a salary entry and its activity log commit together.
/// </summary>
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : DbContext(options), IAuthDbContext, ITransactionalDbContext
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();

    public DbSet<SalaryEntry> SalaryEntries => Set<SalaryEntry>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
    }
}
