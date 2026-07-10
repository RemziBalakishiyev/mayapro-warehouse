using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure;

/// <summary>
/// The Auth module's DbContext. Owns the <c>identity</c> schema and nothing else — no other module's
/// tables are visible here.
/// </summary>
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : DbContext(options), IAuthDbContext
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
    }
}
