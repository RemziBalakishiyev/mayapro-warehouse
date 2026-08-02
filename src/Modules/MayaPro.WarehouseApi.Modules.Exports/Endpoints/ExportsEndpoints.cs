using MayaPro.WarehouseApi.Modules.Exports.Application;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportProductLabelsPdf;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportProductsExcel;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportProductsTemplate;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportSaleInvoicePdf;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportSalesPdf;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Exports.Endpoints;

internal static class ExportsEndpoints
{
    // Matches the host's rate-limit policy: the anonymous invoice endpoint is capped per IP.
    private const string PublicInvoiceRateLimit = "PublicInvoice";

    public static void MapExportsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/exports")
            .WithTags("Exports")
            .RequireAuthorization(); // every authenticated role, including sellers

        group.MapGet("/products.xlsx", async (ExportProductsExcelHandler handler, CancellationToken ct) =>
            {
                ExportFileResult file = await handler.Handle(ct);
                return Results.File(file.Content, file.ContentType, file.FileName);
            })
            .WithName("ExportProductsExcel");

        // The blank template for the Products import flow — same reach as the catalogue export (every
        // authenticated role): a seller may prepare the file even though only Owner/Manager can commit it.
        group.MapGet("/products-template.xlsx", async (ExportProductsTemplateHandler handler, CancellationToken ct) =>
            {
                ExportFileResult file = await handler.Handle(ct);
                return Results.File(file.Content, file.ContentType, file.FileName);
            })
            .WithName("ExportProductsTemplate");

        group.MapGet("/sales.pdf", async (
                string? from,
                string? to,
                ExportSalesPdfHandler handler,
                CancellationToken ct) =>
            {
                if (!OptionalDateQuery.TryParse(from, out DateOnly? fromDate, out string? fromError))
                    return Results.BadRequest(new { code = "Exports.InvalidFrom", message = fromError });
                if (!OptionalDateQuery.TryParse(to, out DateOnly? toDate, out string? toError))
                    return Results.BadRequest(new { code = "Exports.InvalidTo", message = toError });

                Result<ExportFileResult> result = await handler.Handle(fromDate, toDate, ct);
                if (result.IsFailure)
                    return result.ToHttpResult();

                ExportFileResult file = result.Value;
                return Results.File(file.Content, file.ContentType, file.FileName);
            })
            .WithName("ExportSalesPdf");

        // The body is optional at the binding level on purpose: an empty or null body then reaches the
        // handler and comes back as the usual { code, message } 400, not the framework's bare 400.
        group.MapPost("/products/labels.pdf", async (
                LabelsPdfRequest? request,
                ExportProductLabelsPdfHandler handler,
                CancellationToken ct) =>
            {
                Result<ExportFileResult> result = await handler.Handle(request, ct);
                if (result.IsFailure)
                    return result.ToHttpResult();

                ExportFileResult file = result.Value;
                return Results.File(file.Content, file.ContentType, file.FileName);
            })
            .WithName("ExportProductLabelsPdf");

        group.MapGet("/sales/{id:guid}/invoice.pdf", async (
                Guid id,
                ExportSaleInvoicePdfHandler handler,
                CancellationToken ct) =>
            {
                Result<ExportFileResult> result = await handler.Handle(id, ct);
                if (result.IsFailure)
                    return result.ToHttpResult();

                ExportFileResult file = result.Value;
                return Results.File(file.Content, file.ContentType, file.FileName);
            })
            .WithName("ExportSaleInvoicePdf");

        // The one anonymous surface: tokenised invoice links shared over WhatsApp. Same pattern as the
        // Auth module's login — no group-level auth, explicit AllowAnonymous; rate-limited per IP instead.
        endpoints.MapGroup("/api/public/invoices")
            .WithTags("Public")
            .MapGet("/{token}", async (
                string token,
                HttpContext http,
                PublicInvoicePdfHandler handler,
                CancellationToken ct) =>
            {
                Result<ExportFileResult> result = await handler.Handle(token, ct);
                if (result.IsFailure)
                    return result.ToHttpResult();

                // Inline, not attachment — the phone browser should render the PDF, not download it.
                ExportFileResult file = result.Value;
                http.Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
                return Results.File(file.Content, file.ContentType);
            })
            .AllowAnonymous()
            .RequireRateLimiting(PublicInvoiceRateLimit)
            .WithName("GetPublicInvoicePdf");
    }
}
