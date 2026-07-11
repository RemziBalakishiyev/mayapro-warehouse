using FluentValidation;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpense;
using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.GetExpenses;
using MayaPro.WarehouseApi.Modules.Expenses.Endpoints;
using MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MayaPro.WarehouseApi.Modules.Expenses;

/// <summary>
/// The Expenses module: business expenses, and the expense → product real-cost chain. Owns the
/// <c>expenses</c> schema. Starts empty — expenses are not seeded.
/// </summary>
public sealed class ExpensesModule : IModule
{
    public string Name => "Expenses";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scoped options so each scope binds the shared connection from IDbConnectionFactory.
        services.AddDbContext<ExpensesDbContext>((sp, options) =>
        {
            var connection = sp.GetRequiredService<IDbConnectionFactory>().GetConnection();
            options.UseSqlServer(
                connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ExpensesDbContext.Schema));
            options.AddInterceptors(new AuditInterceptor());
        }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);
        services.AddScoped<IExpensesDbContext>(sp => sp.GetRequiredService<ExpensesDbContext>());
        services.AddScoped<ITransactionalDbContext>(sp => sp.GetRequiredService<ExpensesDbContext>());

        services.AddScoped<IValidator<CreateExpenseCommand>, CreateExpenseValidator>();

        services.AddScoped<GetExpensesHandler>();
        services.AddScoped<CreateExpenseHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapExpensesEndpoints();
    }

    public async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ExpensesDbContext>();
        await db.Database.MigrateAsync();
    }
}
