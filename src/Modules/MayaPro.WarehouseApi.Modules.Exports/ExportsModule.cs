using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportProductsExcel;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportSalesPdf;
using MayaPro.WarehouseApi.Modules.Exports.Endpoints;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Exports;

/// <summary>
/// The Exports module: Excel/PDF downloads built from other modules' public contracts. Owns <b>no</b>
/// tables — same pattern as Reports.
/// </summary>
public sealed class ExportsModule : IModule
{
    public string Name => "Exports";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Community license — free for individuals / orgs under $1M revenue (see QuestPDF docs).
        QuestPDF.Settings.License = LicenseType.Community;

        services.AddScoped<ExportProductsExcelHandler>();
        services.AddScoped<ExportSalesPdfHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapExportsEndpoints();
    }

    // No database of its own — nothing to migrate.
    public Task MigrateAsync(IServiceProvider services) => Task.CompletedTask;
}
