using MayaPro.WarehouseApi.Modules.Tenancy.Application;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MayaPro.WarehouseApi.Modules.Tenancy;

/// <summary>
/// The Tenancy module (BE#35, Mərhələ 1): the registry of shops the whole system is partitioned by. Owns
/// the <c>tenancy</c> schema.
/// <para>
/// It deliberately exposes <b>no HTTP endpoints</b>. Mərhələ 1 only isolates existing data; tenant
/// registration and administration are Mərhələ 2, billing Mərhələ 3. Other modules reach it exclusively
/// through <see cref="ITenantDirectory"/>.
/// </para>
/// </summary>
public sealed class TenancyModule : IModule
{
    public string Name => "Tenancy";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scoped options so each scope binds the shared connection from IDbConnectionFactory — same as
        // every other module, even though tenancy never joins a business transaction.
        services.AddDbContext<TenancyDbContext>((sp, options) =>
        {
            var connection = sp.GetRequiredService<IDbConnectionFactory>().GetConnection();
            options.UseSqlServer(
                connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", TenancyDbContext.Schema));
            options.AddInterceptors(new AuditInterceptor());
        }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);

        // Cross-module contract: "does this tenant exist and may it sign in?"
        services.AddScoped<ITenantDirectory, TenantDirectory>();
    }

    // No endpoints in Mərhələ 1 — see the type remarks.
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }

    public async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<TenancyDbContext>();
        await db.Database.MigrateAsync();
    }
}
