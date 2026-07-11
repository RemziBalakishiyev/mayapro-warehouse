using FluentValidation;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.AddSupplierDebt;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.AddSupplierPayment;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.CreateSupplier;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.GetSupplierPayments;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.GetSuppliers;
using MayaPro.WarehouseApi.Modules.Suppliers.Endpoints;
using MayaPro.WarehouseApi.Modules.Suppliers.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MayaPro.WarehouseApi.Modules.Suppliers;

/// <summary>
/// The Suppliers module: suppliers, the debt we owe them, purchases (debts) and payments. Owns the
/// <c>suppliers</c> schema.
/// </summary>
public sealed class SuppliersModule : IModule
{
    public string Name => "Suppliers";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scoped options so each scope binds the shared connection from IDbConnectionFactory.
        services.AddDbContext<SuppliersDbContext>((sp, options) =>
        {
            var connection = sp.GetRequiredService<IDbConnectionFactory>().GetConnection();
            options.UseSqlServer(
                connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", SuppliersDbContext.Schema));
            options.AddInterceptors(new AuditInterceptor());
        }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);
        services.AddScoped<ISuppliersDbContext>(sp => sp.GetRequiredService<SuppliersDbContext>());
        services.AddScoped<ITransactionalDbContext>(sp => sp.GetRequiredService<SuppliersDbContext>());

        services.AddScoped<SupplierSeeder>();

        services.AddScoped<IValidator<CreateSupplierCommand>, CreateSupplierValidator>();
        services.AddScoped<IValidator<AddSupplierDebtCommand>, AddSupplierDebtValidator>();
        services.AddScoped<IValidator<AddSupplierPaymentCommand>, AddSupplierPaymentValidator>();

        services.AddScoped<GetSuppliersHandler>();
        services.AddScoped<CreateSupplierHandler>();
        services.AddScoped<AddSupplierDebtHandler>();
        services.AddScoped<AddSupplierPaymentHandler>();
        services.AddScoped<GetSupplierPaymentsHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSuppliersEndpoints();
    }

    public async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<SuppliersDbContext>();
        await db.Database.MigrateAsync();

        var environment = services.GetRequiredService<IHostEnvironment>();
        if (environment.IsDevelopment())
        {
            var seeder = services.GetRequiredService<SupplierSeeder>();
            await seeder.SeedAsync();
        }
    }
}
