using FluentValidation;
using MayaPro.WarehouseApi.Modules.Settings.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Settings.Application.UseCases.GetSettings;
using MayaPro.WarehouseApi.Modules.Settings.Application.UseCases.UpdateSettings;
using MayaPro.WarehouseApi.Modules.Settings.Endpoints;
using MayaPro.WarehouseApi.Modules.Settings.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MayaPro.WarehouseApi.Modules.Settings;

/// <summary>
/// The Settings module: the store's singleton configuration. Owns the <c>settings</c> schema. Reads are
/// open to every role; changes are owner-only.
/// </summary>
public sealed class SettingsModule : IModule
{
    public string Name => "Settings";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scoped options so each scope binds the shared connection from IDbConnectionFactory.
        services.AddDbContext<SettingsDbContext>((sp, options) =>
        {
            var connection = sp.GetRequiredService<IDbConnectionFactory>().GetConnection();
            options.UseSqlServer(
                connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", SettingsDbContext.Schema));
            options.AddInterceptors(new AuditInterceptor());
        }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);
        services.AddScoped<ISettingsDbContext>(sp => sp.GetRequiredService<SettingsDbContext>());

        services.AddScoped<IValidator<UpdateSettingsCommand>, UpdateSettingsValidator>();

        services.AddScoped<GetSettingsHandler>();
        services.AddScoped<UpdateSettingsHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSettingsEndpoints();
    }

    public async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<SettingsDbContext>();
        await db.Database.MigrateAsync();
    }
}
