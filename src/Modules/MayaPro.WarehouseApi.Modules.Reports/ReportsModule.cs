using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDashboard;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSummary;
using MayaPro.WarehouseApi.Modules.Reports.Endpoints;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MayaPro.WarehouseApi.Modules.Reports;

/// <summary>
/// The Reports module: read-only dashboards and summaries. Owns <b>no</b> tables — every figure is
/// computed on the fly from the other modules' public query contracts, per the architecture decision.
/// </summary>
public sealed class ReportsModule : IModule
{
    public string Name => "Reports";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<GetDashboardHandler>();
        services.AddScoped<GetSummaryHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapReportsEndpoints();
    }

    // No database of its own — nothing to migrate.
    public Task MigrateAsync(IServiceProvider services) => Task.CompletedTask;
}
