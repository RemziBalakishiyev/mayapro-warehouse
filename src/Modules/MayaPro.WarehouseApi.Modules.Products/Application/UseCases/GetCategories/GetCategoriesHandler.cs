using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.GetCategories;

/// <summary>Returns every managed category, ordered by name.</summary>
public sealed class GetCategoriesHandler(IProductsDbContext db)
{
    public async Task<IReadOnlyList<CategoryDto>> Handle(CancellationToken ct)
    {
        return await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name))
            .ToListAsync(ct);
    }
}
