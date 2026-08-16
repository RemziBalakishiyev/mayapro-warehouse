using FluentValidation;
using MayaPro.WarehouseApi.Modules.Auth.Application;
using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.CreateSalaryEntry;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.DeleteSalaryEntry;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetEmployees;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetMe;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetSalaryEntries;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetSalarySummary;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.Login;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.SetEmployeeSalary;
using MayaPro.WarehouseApi.Modules.Auth.Endpoints;
using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MayaPro.WarehouseApi.Modules.Auth;

/// <summary>
/// The Auth module: users, login, JWT and roles. Owns the <c>identity</c> schema.
/// </summary>
public sealed class AuthModule : IModule
{
    public string Name => "Auth";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.Secret.Length >= 32, "Jwt:Secret ən azı 32 simvol olmalıdır")
            .ValidateOnStart();

        // Scoped options so each scope binds the shared connection from IDbConnectionFactory. BE#28 moved
        // this off its own connection string: a salary entry and its activity log have to commit in one
        // transaction, which is only possible while every participating context shares one connection.
        services.AddDbContext<AuthDbContext>((sp, options) =>
        {
            var connection = sp.GetRequiredService<IDbConnectionFactory>().GetConnection();
            options.UseSqlServer(
                connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema));
            options.AddInterceptors(new AuditInterceptor());
            // BE#35: stamps TenantId on insert and freezes it afterwards — the write-side of isolation.
            options.AddInterceptors(new TenantInterceptor(sp.GetRequiredService<ICurrentTenant>()));
        }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);
        services.AddScoped<IAuthDbContext>(sp => sp.GetRequiredService<AuthDbContext>());
        services.AddScoped<ITransactionalDbContext>(sp => sp.GetRequiredService<AuthDbContext>());

        // Cross-module contract: salary payments are cash out of the drawer, so day-end and the dashboard
        // read them the same way they read expenses.
        services.AddScoped<ISalaryModule, SalaryModuleContract>();

        // BE#36 cross-module contract: how the Tenancy module gets a new shop's first Sahibkar created
        // (and asks whether a phone is already a login anywhere on the platform).
        services.AddScoped<IIdentityProvisioning, IdentityProvisioningContract>();

        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<UserSeeder>();
        services.AddScoped<PlatformAdminSeeder>();

        services.AddScoped<IValidator<LoginCommand>, LoginValidator>();
        services.AddScoped<IValidator<SetEmployeeSalaryCommand>, SetEmployeeSalaryValidator>();
        services.AddScoped<IValidator<CreateSalaryEntryCommand>, CreateSalaryEntryValidator>();

        services.AddScoped<LoginHandler>();
        services.AddScoped<GetMeHandler>();
        services.AddScoped<GetEmployeesHandler>();
        services.AddScoped<SetEmployeeSalaryHandler>();
        services.AddScoped<CreateSalaryEntryHandler>();
        services.AddScoped<GetSalaryEntriesHandler>();
        services.AddScoped<DeleteSalaryEntryHandler>();
        services.AddScoped<GetSalarySummaryHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuthEndpoints();
        endpoints.MapEmployeesEndpoints();
    }

    public async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AuthDbContext>();
        await db.Database.MigrateAsync();

        var environment = services.GetRequiredService<IHostEnvironment>();
        if (environment.IsDevelopment())
        {
            var seeder = services.GetRequiredService<UserSeeder>();
            await seeder.SeedAsync();
        }

        // BE#36: unlike the demo users, the platform admin is seeded in every environment — a fresh
        // production install would otherwise have no way into /api/admin/*. Idempotent, and a no-op when
        // the PlatformAdmin section is not configured.
        var platformAdminSeeder = services.GetRequiredService<PlatformAdminSeeder>();
        await platformAdminSeeder.SeedAsync();
    }
}
