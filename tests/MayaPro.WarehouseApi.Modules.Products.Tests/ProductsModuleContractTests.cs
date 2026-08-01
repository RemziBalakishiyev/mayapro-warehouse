using MayaPro.WarehouseApi.Modules.Products.Application;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Tests;

/// <summary>
/// Unit tests for <see cref="ProductsModuleContract.GetStockAdjustmentsAsync"/> (BE#27, AC-G6): the new
/// <c>IProductsModule</c> contract member the products-kpi endpoint reads back the manual stock corrections
/// through. Uses a real (in-memory) <see cref="ProductsDbContext"/> so the date-range SQL filter itself is
/// exercised, not just the calculator.
/// </summary>
public sealed class ProductsModuleContractTests
{
    private static readonly DateOnly Today = new(2026, 8, 2);

    [Fact]
    public async Task Returns_Adjustments_Within_The_Inclusive_Range_Only()
    {
        await using ProductsDbContext db = NewDb();
        Guid productId = Guid.NewGuid();
        AddAdjustment(db, productId, delta: 5, date: Today.AddDays(-1));
        AddAdjustment(db, productId, delta: 10, date: Today);
        AddAdjustment(db, productId, delta: -3, date: Today.AddDays(1)); // outside the range
        await db.SaveChangesAsync();

        var contract = new ProductsModuleContract(db, new FakeDateProvider());

        IReadOnlyList<StockAdjustmentRow> rows =
            await contract.GetStockAdjustmentsAsync(Today.AddDays(-1), Today, default);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.True(r.Date <= Today));
    }

    [Fact]
    public async Task Empty_From_To_Returns_Every_Adjustment()
    {
        await using ProductsDbContext db = NewDb();
        Guid productId = Guid.NewGuid();
        AddAdjustment(db, productId, delta: 5, date: Today.AddDays(-100));
        AddAdjustment(db, productId, delta: -2, date: Today.AddDays(50));
        await db.SaveChangesAsync();

        var contract = new ProductsModuleContract(db, new FakeDateProvider());

        IReadOnlyList<StockAdjustmentRow> rows = await contract.GetStockAdjustmentsAsync(null, null, default);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task Preserves_The_Signed_Delta_Positive_And_Negative()
    {
        await using ProductsDbContext db = NewDb();
        Guid productId = Guid.NewGuid();
        AddAdjustment(db, productId, delta: -7, date: Today);
        await db.SaveChangesAsync();

        var contract = new ProductsModuleContract(db, new FakeDateProvider());

        IReadOnlyList<StockAdjustmentRow> rows = await contract.GetStockAdjustmentsAsync(null, null, default);

        StockAdjustmentRow row = Assert.Single(rows);
        Assert.Equal(-7, row.Delta);
        Assert.Equal(productId, row.ProductId);
    }

    /// <summary>
    /// Adds a stock adjustment stamped with an explicit business-zone date — <see cref="StockAdjustment.Create"/>
    /// always uses "now", so the test overrides the tracked entity's <c>Date</c> the same way
    /// <c>GetOpenDebtsHandlerTests</c> stamps payments/adjustments in the Customers module's tests.
    /// </summary>
    private static void AddAdjustment(ProductsDbContext db, Guid productId, int delta, DateOnly date)
    {
        StockAdjustment adjustment = StockAdjustment.Create(productId, delta);
        db.StockAdjustments.Add(adjustment);
        db.Entry(adjustment).Property(a => a.Date).CurrentValue =
            date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    private static ProductsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase($"products-contract-tests-{Guid.NewGuid()}")
            .Options;
        return new ProductsDbContext(options);
    }
}
