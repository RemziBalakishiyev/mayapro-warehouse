using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CommitProductsImport;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.PreviewProductsImport;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Products.Endpoints;

/// <summary>The two-step Excel import flow (preview, then commit) for the products catalogue.</summary>
internal static class ImportsEndpoints
{
    // Matches the host's role policy: only Owner or Manager may bulk-add/edit stock items, same reach as
    // creating/editing a single product.
    private const string OwnerOrManager = "OwnerOrManager";

    public static void MapImportsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/imports")
            .WithTags("Imports")
            .RequireAuthorization(OwnerOrManager);

        group.MapPost("/products/preview", async (
                IFormFile? file,
                PreviewProductsImportHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(file, ct);
                return result.ToHttpResult();
            })
            // A form-file parameter makes ASP.NET Core require antiforgery middleware by default (CSRF
            // protection for browser form posts). This API is a stateless JWT bearer API with no cookies/
            // antiforgery infrastructure at all — the JWT itself is the CSRF defence — so it is disabled
            // explicitly here, the same way a SPA's API client uploads any other file.
            .DisableAntiforgery()
            .WithName("PreviewProductsImport");

        group.MapPost("/products/commit", async (
                CommitProductsImportCommand command,
                CommitProductsImportHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                return result.ToHttpResult();
            })
            .WithName("CommitProductsImport");
    }
}
