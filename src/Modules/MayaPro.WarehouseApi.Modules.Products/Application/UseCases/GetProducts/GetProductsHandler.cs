using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.GetProducts;

/// <summary>Returns every product, newest first.</summary>
public sealed class GetProductsHandler(IProductsDbContext db)
{
    public async Task<IReadOnlyList<ProductDto>> Handle(CancellationToken ct)
    {
        List<Product> products = await db.Products
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return products.Select(p => p.ToDto()).ToList();
    }
}
