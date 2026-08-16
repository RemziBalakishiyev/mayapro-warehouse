using FluentValidation;
using MayaPro.WarehouseApi.Modules.Tenancy.Application;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.ApproveTenant;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.CreateTenant;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.GetPlatformStats;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.GetTenantPayments;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.GetTenants;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.RecordPayment;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.SetTenantStatus;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.UseCases.RegisterTenant;
using MayaPro.WarehouseApi.Modules.Tenancy.Endpoints;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MayaPro.WarehouseApi.Modules.Tenancy;

/// <summary>
/// The Tenancy module: the registry of shops the whole system is partitioned by, and (BE#36) their
/// subscriptions. Owns the <c>tenancy</c> schema.
/// <para>
/// Mərhələ 1 (BE#35) exposed no HTTP surface at all. Mərhələ 2 adds two: anonymous shop registration
/// (<c>POST /api/auth/register</c>) and the platform operator's console (<c>/api/admin/*</c>, policy
/// <see cref="PlatformAdminAccess.Policy"/>). Other modules still reach the registry only through
/// <see cref="ITenantDirectory"/>.
/// </para>
/// </summary>
public sealed class TenancyModule : IModule
{
    public string Name => "Tenancy";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Scoped options so each scope binds the shared connection from IDbConnectionFactory — required now
        // that registration commits a tenant row and an identity row in one transaction.
        services.AddDbContext<TenancyDbContext>((sp, options) =>
        {
            var connection = sp.GetRequiredService<IDbConnectionFactory>().GetConnection();
            options.UseSqlServer(
                connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", TenancyDbContext.Schema));
            options.AddInterceptors(new AuditInterceptor());
            // Deliberately no TenantInterceptor: neither table is tenant-scoped (see SubscriptionPayment).
        }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);
        services.AddScoped<ITransactionalDbContext>(sp => sp.GetRequiredService<TenancyDbContext>());

        // Cross-module contract: "does this tenant exist, may it sign in, and until when?"
        services.AddScoped<ITenantDirectory, TenantDirectory>();

        services.AddScoped<TenantProvisioning>();

        services.AddScoped<IValidator<RegisterTenantCommand>, RegisterTenantValidator>();
        services.AddScoped<IValidator<CreateTenantCommand>, CreateTenantValidator>();
        services.AddScoped<IValidator<ApproveTenantCommand>, ApproveTenantValidator>();
        services.AddScoped<IValidator<RecordPaymentCommand>, RecordPaymentValidator>();

        services.AddScoped<RegisterTenantHandler>();
        services.AddScoped<GetTenantsHandler>();
        services.AddScoped<CreateTenantHandler>();
        services.AddScoped<ApproveTenantHandler>();
        services.AddScoped<SetTenantStatusHandler>();
        services.AddScoped<RecordPaymentHandler>();
        services.AddScoped<GetTenantPaymentsHandler>();
        services.AddScoped<GetPlatformStatsHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapTenantRegistrationEndpoints();
        endpoints.MapAdminTenantsEndpoints();
    }

    public async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<TenancyDbContext>();
        await db.Database.MigrateAsync();
    }
}
