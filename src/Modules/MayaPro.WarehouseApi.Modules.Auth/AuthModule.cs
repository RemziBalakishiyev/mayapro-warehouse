using FluentValidation;
using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetEmployees;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetMe;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.Login;
using MayaPro.WarehouseApi.Modules.Auth.Endpoints;
using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
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

        services.AddDbContext<AuthDbContext>((sp, options) =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("Default"),
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema));
            options.AddInterceptors(new AuditInterceptor());
        });
        services.AddScoped<IAuthDbContext>(sp => sp.GetRequiredService<AuthDbContext>());

        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<UserSeeder>();

        services.AddScoped<IValidator<LoginCommand>, LoginValidator>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<GetMeHandler>();
        services.AddScoped<GetEmployeesHandler>();
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
    }
}
