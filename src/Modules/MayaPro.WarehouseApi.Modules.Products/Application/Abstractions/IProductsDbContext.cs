using MayaPro.WarehouseApi.Modules.Products.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;

/// <summary>The Products module's data surface. Handlers depend on this, not on the concrete DbContext.</summary>
public interface IProductsDbContext
{
    DbSet<Product> Products { get; }

    DbSet<Category> Categories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
