using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Infrastructure;

/// <summary>
/// The Products module's DbContext. Owns the <c>products</c> schema and nothing else — no other module's
/// tables are visible here.
/// </summary>
public sealed class ProductsDbContext(DbContextOptions<ProductsDbContext> options)
    : DbContext(options), IProductsDbContext
{
    public const string Schema = "products";

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductsDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All money is decimal(18,2) by convention across the module.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
