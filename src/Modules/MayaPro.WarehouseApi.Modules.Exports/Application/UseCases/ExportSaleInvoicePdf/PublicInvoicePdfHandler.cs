using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportSaleInvoicePdf;

/// <summary>
/// The anonymous invoice endpoint's handler: resolves the public token to a sale (Sales contract) and
/// reuses the authenticated invoice generator. Every failure — junk token, unknown token, or the sale
/// vanishing between lookup and render — collapses to the same NotFound, so the public surface leaks
/// nothing about which tokens exist.
/// <para>
/// BE#35 — how an anonymous request stays tenant-safe. The token lookup is the single point that sees
/// across tenants, and it hands back the owning tenant with the sale id. Everything after that runs inside
/// <see cref="TenantScope"/>, i.e. with the ordinary global query filters active and pinned to that tenant:
/// the sale, the customer block and the store header on the PDF can only come from the shop that issued the
/// link. No JWT is involved and none is needed.
/// </para>
/// </summary>
public sealed class PublicInvoicePdfHandler(
    ISalesModule sales,
    ExportSaleInvoicePdfHandler invoicePdf,
    TenantScope tenantScope)
{
    private static readonly Error InvoiceNotFound = new("Exports.InvoiceNotFound", "Faktura tapılmadı");

    // Tokens are 43-char Base64Url strings; anything far off is junk we can reject without a query.
    private const int MaxTokenLength = 64;

    public async Task<Result<ExportFileResult>> Handle(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
            return Result.Failure<ExportFileResult>(InvoiceNotFound);

        InvoiceTokenOwner? owner = await sales.GetInvoiceTokenOwnerAsync(token, ct);
        if (owner is null)
            return Result.Failure<ExportFileResult>(InvoiceNotFound);

        using IDisposable _ = tenantScope.Use(owner.TenantId);

        Result<ExportFileResult> result = await invoicePdf.Handle(owner.SaleId, ct);
        return result.IsFailure
            ? Result.Failure<ExportFileResult>(InvoiceNotFound)
            : result;
    }
}
