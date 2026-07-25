using System.Security.Cryptography;
using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateInvoiceLink;

/// <summary>
/// Returns the sale's public invoice URL, minting the token on first request: 32 cryptographically random
/// bytes, Base64Url-encoded (43 chars). Repeat requests return the same token, so a link shared over
/// WhatsApp never goes stale.
/// </summary>
public sealed class CreateInvoiceLinkHandler(ISalesDbContext db, PublicLinkOptions options)
{
    public async Task<Result<InvoiceLinkDto>> Handle(Guid saleId, CancellationToken ct)
    {
        Sale? sale = await db.Sales.FirstOrDefaultAsync(s => s.Id == saleId, ct);
        if (sale is null)
            return Result.Failure<InvoiceLinkDto>(SaleErrors.NotFound);

        if (sale.InvoiceToken is null)
        {
            sale.AssignInvoiceToken(GenerateToken());
            await db.SaveChangesAsync(ct);
        }

        string url = $"{options.PublicBaseUrl.TrimEnd('/')}/api/public/invoices/{sale.InvoiceToken}";
        return Result.Success(new InvoiceLinkDto(url));
    }

    private static string GenerateToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
