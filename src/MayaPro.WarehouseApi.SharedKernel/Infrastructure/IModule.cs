using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MayaPro.WarehouseApi.SharedKernel.Infrastructure;

/// <summary>
/// Contract every feature module implements. The API host discovers modules by reflection and
/// wires them into the pipeline: services first, then endpoints, then migrations on startup.
/// </summary>
public interface IModule
{
    /// <summary>Human-readable module name, used in logs and diagnostics.</summary>
    string Name { get; }

    /// <summary>Register this module's services (DbContext, handlers, validators, contract implementations).</summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Map this module's endpoints (typically under a <c>/api/...</c> group).</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);

    /// <summary>Apply this module's own EF Core migrations. No-op for modules without a database.</summary>
    Task MigrateAsync(IServiceProvider services);
}
