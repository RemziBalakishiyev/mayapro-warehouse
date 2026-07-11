using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.GetProduct;

/// <summary>Returns a single product by id, or a not-found business error.</summary>
public sealed class GetProductHandler(IProductsDbContext db)
{
    public async Task<Result<ProductDto>> Handle(Guid id, CancellationToken ct)
    {
        Product? product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return product is null
            ? Result.Failure<ProductDto>(ProductErrors.NotFound)
            : Result.Success(product.ToDto());
    }
}
