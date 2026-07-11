using FluentValidation;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.UseCases.CloseDay;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.UseCases.GetClosings;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.UseCases.GetTodayClosing;
using MayaPro.WarehouseApi.Modules.DayEnd.Endpoints;
using MayaPro.WarehouseApi.Modules.DayEnd.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MayaPro.WarehouseApi.Modules.DayEnd;

/// <summary>
/// The DayEnd module: day-end cash reconciliation (closings). Owns the <c>dayend</c> schema. Totals come
/// from the Sales and Expenses modules via their contracts.
/// </summary>
public sealed class DayEndModule : IModule
{
    public string Name => "DayEnd";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scoped options so each scope binds the shared connection from IDbConnectionFactory.
        services.AddDbContext<DayEndDbContext>((sp, options) =>
        {
            var connection = sp.GetRequiredService<IDbConnectionFactory>().GetConnection();
            options.UseSqlServer(
                connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", DayEndDbContext.Schema));
            options.AddInterceptors(new AuditInterceptor());
        }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);
        services.AddScoped<IDayEndDbContext>(sp => sp.GetRequiredService<DayEndDbContext>());
        services.AddScoped<ITransactionalDbContext>(sp => sp.GetRequiredService<DayEndDbContext>());

        services.AddScoped<IValidator<CloseDayCommand>, CloseDayValidator>();

        services.AddScoped<CloseDayHandler>();
        services.AddScoped<GetClosingsHandler>();
        services.AddScoped<GetTodayClosingHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDayEndEndpoints();
    }

    public async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<DayEndDbContext>();
        await db.Database.MigrateAsync();
    }
}
