using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.GetSaleById;

/// <summary>
/// Returns the full detail of one sale, or a not-found error (→ 404). Enriches the stored sale with two
/// cross-module lookups: the customer's name for a credit sale (Customers contract) and the product's
/// current catalogue name for a catalogued sale (Products contract). Both are best-effort — a since-deleted
/// customer or product simply leaves the corresponding name null; the sale-time snapshots always remain.
/// </summary>
public sealed class GetSaleByIdHandler(ISalesDbContext db, ICustomersModule customers, IProductsModule products)
{
    public async Task<Result<SaleDetailDto>> Handle(Guid id, CancellationToken ct)
    {
        Sale? sale = await db.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sale is null)
            return Result.Failure<SaleDetailDto>(SaleErrors.NotFound);

        string? customerName = null;
        if (sale.CustomerId is { } customerId)
        {
            Dictionary<Guid, string> names = await customers.GetNamesAsync(new[] { customerId }, ct);
            customerName = names.GetValueOrDefault(customerId);
        }

        string? currentProductName = null;
        if (sale.ProductId is { } productId)
        {
            Result<ProductSnapshot> snapshot = await products.GetSnapshotAsync(productId, ct);
            if (snapshot.IsSuccess)
                currentProductName = snapshot.Value.Name;
        }

        return Result.Success(sale.ToDetailDto(customerName, currentProductName));
    }
}
