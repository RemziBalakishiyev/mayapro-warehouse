using FluentValidation;
using MayaPro.WarehouseApi.Modules.Products.Application;
using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.AdjustStock;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CreateProduct;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.GetProduct;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.GetProducts;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.UpdateProduct;
using MayaPro.WarehouseApi.Modules.Products.Endpoints;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MayaPro.WarehouseApi.Modules.Products;

/// <summary>
/// The Products module: product catalogue, real-cost domain logic, CRUD and stock adjustment. Owns the
/// <c>products</c> schema.
/// </summary>
public sealed class ProductsModule : IModule
{
    public string Name => "Products";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scoped options so each scope binds the shared connection from IDbConnectionFactory.
        services.AddDbContext<ProductsDbContext>((sp, options) =>
        {
            var connection = sp.GetRequiredService<IDbConnectionFactory>().GetConnection();
            options.UseSqlServer(
                connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ProductsDbContext.Schema));
            options.AddInterceptors(new AuditInterceptor());
        }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);
        services.AddScoped<IProductsDbContext>(sp => sp.GetRequiredService<ProductsDbContext>());
        services.AddScoped<ITransactionalDbContext>(sp => sp.GetRequiredService<ProductsDbContext>());

        // Cross-module contract: stock decrement for the sales chain.
        services.AddScoped<IProductsModule, ProductsModuleContract>();

        services.AddScoped<ProductSeeder>();

        services.AddScoped<IValidator<CreateProductCommand>, CreateProductValidator>();
        services.AddScoped<IValidator<UpdateProductCommand>, UpdateProductValidator>();
        services.AddScoped<IValidator<AdjustStockCommand>, AdjustStockValidator>();

        services.AddScoped<GetProductsHandler>();
        services.AddScoped<GetProductHandler>();
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<UpdateProductHandler>();
        services.AddScoped<AdjustStockHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapProductsEndpoints();
    }

    public async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ProductsDbContext>();
        await db.Database.MigrateAsync();

        var environment = services.GetRequiredService<IHostEnvironment>();
        if (environment.IsDevelopment())
        {
            var seeder = services.GetRequiredService<ProductSeeder>();
            await seeder.SeedAsync();
        }
    }
}
