using MayaPro.WarehouseApi.Modules.Exports.Application;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportProductLabelsPdf;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportProductsExcel;
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

        group.MapGet("/sales.pdf", async (
                string? from,
                string? to,
                ExportSalesPdfHandler handler,
                CancellationToken ct) =>
            {
                if (!TryParseOptionalDate(from, out DateOnly? fromDate, out string? fromError))
                    return Results.BadRequest(new { code = "Exports.InvalidFrom", message = fromError });
                if (!TryParseOptionalDate(to, out DateOnly? toDate, out string? toError))
                    return Results.BadRequest(new { code = "Exports.InvalidTo", message = toError });

                Result<ExportFileResult> result = await handler.Handle(fromDate, toDate, ct);
                if (result.IsFailure)
                    return result.ToHttpResult();

                ExportFileResult file = result.Value;
                return Results.File(file.Content, file.ContentType, file.FileName);
            })
            .WithName("ExportSalesPdf");

        group.MapPost("/products/labels.pdf", async (
                LabelsPdfRequest request,
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

    private static bool TryParseOptionalDate(string? raw, out DateOnly? date, out string? error)
    {
        date = null;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (DateOnly.TryParse(raw, out DateOnly parsed))
        {
            date = parsed;
            return true;
        }

        error = "Tarix formatı yanlışdır (gözlənilən: yyyy-MM-dd)";
        return false;
    }
}
