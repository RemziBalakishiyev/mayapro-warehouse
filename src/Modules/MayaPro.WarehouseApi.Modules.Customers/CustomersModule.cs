using FluentValidation;
using MayaPro.WarehouseApi.Modules.Customers.Application;
using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.AddCustomerPayment;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.CreateCustomer;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetCustomerPayments;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetCustomers;
using MayaPro.WarehouseApi.Modules.Customers.Endpoints;
using MayaPro.WarehouseApi.Modules.Customers.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MayaPro.WarehouseApi.Modules.Customers;

/// <summary>
/// The Customers module: customers, their debt, and payments against it. Owns the <c>customers</c> schema.
/// </summary>
public sealed class CustomersModule : IModule
{
    public string Name => "Customers";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scoped options so each scope binds the shared connection from IDbConnectionFactory.
        services.AddDbContext<CustomersDbContext>((sp, options) =>
        {
            var connection = sp.GetRequiredService<IDbConnectionFactory>().GetConnection();
            options.UseSqlServer(
                connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", CustomersDbContext.Schema));
            options.AddInterceptors(new AuditInterceptor());
        }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);
        services.AddScoped<ICustomersDbContext>(sp => sp.GetRequiredService<CustomersDbContext>());
        services.AddScoped<ITransactionalDbContext>(sp => sp.GetRequiredService<CustomersDbContext>());

        // Cross-module contract: debt increase for the credit-sale chain.
        services.AddScoped<ICustomersModule, CustomersModuleContract>();

        services.AddScoped<CustomerSeeder>();

        services.AddScoped<IValidator<CreateCustomerCommand>, CreateCustomerValidator>();
        services.AddScoped<IValidator<AddCustomerPaymentCommand>, AddCustomerPaymentValidator>();

        services.AddScoped<GetCustomersHandler>();
        services.AddScoped<CreateCustomerHandler>();
        services.AddScoped<GetCustomerPaymentsHandler>();
        services.AddScoped<AddCustomerPaymentHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCustomersEndpoints();
    }

    public async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<CustomersDbContext>();
        await db.Database.MigrateAsync();

        var environment = services.GetRequiredService<IHostEnvironment>();
        if (environment.IsDevelopment())
        {
            var seeder = services.GetRequiredService<CustomerSeeder>();
            await seeder.SeedAsync();
        }
    }
}
