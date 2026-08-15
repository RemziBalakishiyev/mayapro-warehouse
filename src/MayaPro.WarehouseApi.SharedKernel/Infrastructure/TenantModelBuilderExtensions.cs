using System.Reflection;
using MayaPro.WarehouseApi.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.SharedKernel.Infrastructure;

/// <summary>
/// The read-side half of tenant isolation, shared by every module: one call in <c>OnModelCreating</c>
/// installs a global query filter on <b>every</b> <see cref="ITenantScoped"/> entity in the model and
/// indexes its tenant column. A module can therefore never forget to scope a new table — deriving the
/// entity from <see cref="TenantEntity"/> is enough.
/// </summary>
public static class TenantModelBuilderExtensions
{
    /// <summary>The shadow-free CLR property name every tenant-scoped entity carries.</summary>
    public const string TenantIdProperty = nameof(ITenantScoped.TenantId);

    private static readonly MethodInfo ApplyOneMethod = typeof(TenantModelBuilderExtensions)
        .GetMethod(nameof(ApplyTenantQueryFilter), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Adds <c>WHERE TenantId = @currentTenant</c> to every tenant-scoped entity in
    /// <paramref name="modelBuilder"/>, and indexes the column.
    /// </summary>
    /// <param name="modelBuilder">The model being built.</param>
    /// <param name="context">
    /// The DbContext being configured — pass <c>this</c>. It must be the context instance itself: EF Core
    /// rewrites context references inside a query filter into the executing context, which is what keeps one
    /// cached model correct while every request still filters by its own tenant. Capturing a service object
    /// instead would freeze the first request's tenant into the model.
    /// </param>
    public static void ApplyTenantIsolation<TContext>(this ModelBuilder modelBuilder, TContext context)
        where TContext : DbContext, ITenantAwareDbContext
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(context);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
                continue;

            // Owned types live inside their owner's row and inherit its tenant; derived types share the
            // root's filter. EF Core rejects a filter on either.
            if (entityType.IsOwned() || entityType.BaseType is not null)
                continue;

            ApplyOneMethod
                .MakeGenericMethod(entityType.ClrType, typeof(TContext))
                .Invoke(null, [modelBuilder, context]);
        }
    }

    private static void ApplyTenantQueryFilter<TEntity, TContext>(ModelBuilder modelBuilder, TContext context)
        where TEntity : class, ITenantScoped
        where TContext : DbContext, ITenantAwareDbContext
    {
        EntityTypeBuilder<TEntity> entity = modelBuilder.Entity<TEntity>();

        // EF.Property rather than e.TenantId: the property is declared on TenantEntity, and going through
        // the metadata name keeps this correct for any future entity that carries the column differently.
        entity.HasQueryFilter(e => EF.Property<Guid>(e, TenantIdProperty) == context.CurrentTenantId);

        // Every tenant-scoped read starts with this predicate, so the column is always worth indexing.
        entity.HasIndex(TenantIdProperty);
    }
}
