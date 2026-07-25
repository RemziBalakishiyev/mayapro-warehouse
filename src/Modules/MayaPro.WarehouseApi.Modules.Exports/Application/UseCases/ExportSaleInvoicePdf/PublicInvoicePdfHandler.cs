using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportSaleInvoicePdf;

/// <summary>
/// The anonymous invoice endpoint's handler: resolves the public token to a sale (Sales contract) and
/// reuses the authenticated invoice generator. Every failure — junk token, unknown token, or the sale
/// vanishing between lookup and render — collapses to the same NotFound, so the public surface leaks
/// nothing about which tokens exist.
/// </summary>
public sealed class PublicInvoicePdfHandler(ISalesModule sales, ExportSaleInvoicePdfHandler invoicePdf)
{
    private static readonly Error InvoiceNotFound = new("Exports.InvoiceNotFound", "Faktura tapılmadı");

    // Tokens are 43-char Base64Url strings; anything far off is junk we can reject without a query.
    private const int MaxTokenLength = 64;

    public async Task<Result<ExportFileResult>> Handle(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
            return Result.Failure<ExportFileResult>(InvoiceNotFound);

        Guid? saleId = await sales.GetSaleIdByInvoiceTokenAsync(token, ct);
        if (saleId is null)
            return Result.Failure<ExportFileResult>(InvoiceNotFound);

        Result<ExportFileResult> result = await invoicePdf.Handle(saleId.Value, ct);
        return result.IsFailure
            ? Result.Failure<ExportFileResult>(InvoiceNotFound)
            : result;
    }
}
