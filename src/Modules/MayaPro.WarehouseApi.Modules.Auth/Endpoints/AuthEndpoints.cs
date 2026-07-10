using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetMe;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.Login;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Auth.Endpoints;

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        // Login is the one anonymous endpoint in the module.
        group.MapPost("/login", async (
                LoginCommand command,
                LoginHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                return result.ToHttpResult();
            })
            .AllowAnonymous()
            .WithName("Login");

        group.MapGet("/me", async (
                ICurrentUser currentUser,
                GetMeHandler handler,
                CancellationToken ct) =>
            {
                if (currentUser.UserId is not { } userId)
                    return Results.Unauthorized();

                var result = await handler.Handle(userId, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName("GetMe");
    }
}
