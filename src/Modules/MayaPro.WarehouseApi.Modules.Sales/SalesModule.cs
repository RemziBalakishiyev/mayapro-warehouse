using FluentValidation;
using MayaPro.WarehouseApi.Modules.Sales.Application;
using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateInvoiceLink;
using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateSale;
using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.DeleteSale;
using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.GetSaleById;
using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.GetSales;
using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.UpdateSale;
using MayaPro.WarehouseApi.Modules.Sales.Endpoints;
using MayaPro.WarehouseApi.Modules.Sales.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MayaPro.WarehouseApi.Modules.Sales;

/// <summary>
/// The Sales module: the sale chain (stock → debt → sale) in one cross-module transaction. Owns the
/// <c>sales</c> schema. Starts empty — historical sales are not seeded.
/// </summary>
public sealed class SalesModule : IModule
{
    public string Name => "Sales";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scoped options so each scope binds the shared connection from IDbConnectionFactory.
        services.AddDbContext<SalesDbContext>((sp, options) =>
        {
            var connection = sp.GetRequiredService<IDbConnectionFactory>().GetConnection();
            options.UseSqlServer(
                connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", SalesDbContext.Schema));
            options.AddInterceptors(new AuditInterceptor());
            // BE#35: stamps TenantId on insert and freezes it afterwards — the write-side of isolation.
            options.AddInterceptors(new TenantInterceptor(sp.GetRequiredService<ICurrentTenant>()));
        }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);
        services.AddScoped<ISalesDbContext>(sp => sp.GetRequiredService<SalesDbContext>());
        services.AddScoped<ITransactionalDbContext>(sp => sp.GetRequiredService<SalesDbContext>());

        // Cross-module contract: day totals for day-end.
        services.AddScoped<ISalesModule, SalesModuleContract>();

        services.AddScoped<IValidator<CreateSaleCommand>, CreateSaleValidator>();
        services.AddScoped<IValidator<UpdateSaleCommand>, UpdateSaleValidator>();

        // Public invoice links are composed against this base URL (dev: the local http endpoint).
        services.AddSingleton(new PublicLinkOptions(
            configuration["App:PublicBaseUrl"] ?? "http://localhost:5208"));

        services.AddScoped<GetSalesHandler>();
        services.AddScoped<GetSaleByIdHandler>();
        services.AddScoped<CreateSaleHandler>();
        services.AddScoped<UpdateSaleHandler>();
        services.AddScoped<DeleteSaleHandler>();
        services.AddScoped<CreateInvoiceLinkHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSalesEndpoints();
    }

    public async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<SalesDbContext>();
        await db.Database.MigrateAsync();
    }
}
