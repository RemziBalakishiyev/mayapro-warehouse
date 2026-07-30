using MayaPro.WarehouseApi.Modules.Products.Application.Imports;
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
                HttpContext http,
                PreviewProductsImportHandler handler,
                CancellationToken ct) =>
            {
                IFormFile? file = await ReadUploadedFileAsync(http, ct);
                if (file is null)
                    return Result.Failure<ImportPreviewResponse>(ImportErrors.EmptyFile).ToHttpResult();

                // Unwrapping the multipart part is the endpoint's job; the use case only reads a stream.
                await using Stream content = file.OpenReadStream();
                var result = await handler.Handle(content, file.Length, ct);
                return result.ToHttpResult();
            })
            // A form-file parameter makes ASP.NET Core require antiforgery middleware by default (CSRF
            // protection for browser form posts). This API is a stateless JWT bearer API with no cookies/
            // antiforgery infrastructure at all — the JWT itself is the CSRF defence — so it is disabled
            // explicitly here, the same way a SPA's API client uploads any other file.
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data") // keeps the Swagger upload box despite the manual read
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

    /// <summary>
    /// Pulls the uploaded file out of the multipart body, or returns null when there is nothing usable —
    /// no file part, the wrong content type, or a body the form reader cannot parse. Read by hand rather
    /// than through an <c>IFormFile</c> parameter because model binding turns an unreadable body into an
    /// unhandled exception (a 500); for this endpoint "no usable file" is the plain 400 of AC-5.
    /// </summary>
    private static async Task<IFormFile?> ReadUploadedFileAsync(HttpContext http, CancellationToken ct)
    {
        if (!http.Request.HasFormContentType)
            return null;

        try
        {
            IFormFileCollection files = (await http.Request.ReadFormAsync(ct)).Files;
            return files["file"] ?? files.FirstOrDefault();
        }
        catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException or IOException)
        {
            return null;
        }
    }
}
