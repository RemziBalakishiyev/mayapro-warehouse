using MayaPro.WarehouseApi.Modules.Activity.Application;
using MayaPro.WarehouseApi.Modules.Activity.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Activity.Application.UseCases.GetActivity;
using MayaPro.WarehouseApi.Modules.Activity.Endpoints;
using MayaPro.WarehouseApi.Modules.Activity.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MayaPro.WarehouseApi.Modules.Activity;

/// <summary>
/// The Activity module: the activity feed and the real <see cref="IActivityLogger"/>. Owns the
/// <c>activity</c> schema. Registering it swaps every module's logging from the temporary Serilog logger
/// to <see cref="DbActivityLogger"/> — no calling code changes, which was the point of the contract.
/// </summary>
public sealed class ActivityModule : IModule
{
    public string Name => "Activity";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scoped options so each scope binds the shared connection from IDbConnectionFactory.
        services.AddDbContext<ActivityDbContext>((sp, options) =>
        {
            var connection = sp.GetRequiredService<IDbConnectionFactory>().GetConnection();
            options.UseSqlServer(
                connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ActivityDbContext.Schema));
            options.AddInterceptors(new AuditInterceptor());
        }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);
        services.AddScoped<IActivityDbContext>(sp => sp.GetRequiredService<ActivityDbContext>());
        services.AddScoped<ITransactionalDbContext>(sp => sp.GetRequiredService<ActivityDbContext>());

        // The real activity logger — replaces the temporary Serilog one.
        services.AddScoped<IActivityLogger, DbActivityLogger>();

        services.AddScoped<GetActivityHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapActivityEndpoints();
    }

    public async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ActivityDbContext>();
        await db.Database.MigrateAsync();
    }
}
