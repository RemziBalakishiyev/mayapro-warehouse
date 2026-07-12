using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CreateCategory;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Tests;

/// <summary>
/// Unit tests for <see cref="CreateCategoryHandler"/>: the happy path, the empty-name rule, and the
/// duplicate-name rule (which returns <see cref="ProductErrors.CategoryDuplicate"/>).
/// </summary>
public sealed class CreateCategoryHandlerTests
{
    [Fact]
    public async Task Creates_Category_When_Name_Is_New()
    {
        await using ProductsDbContext db = NewDb();
        var handler = new CreateCategoryHandler(db, new CreateCategoryValidator());

        var result = await handler.Handle(new CreateCategoryCommand("Şalvar"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Şalvar", result.Value.Name);
        Assert.Equal(1, await db.Categories.CountAsync());
    }

    [Fact]
    public async Task Trims_Name_Before_Storing()
    {
        await using ProductsDbContext db = NewDb();
        var handler = new CreateCategoryHandler(db, new CreateCategoryValidator());

        var result = await handler.Handle(new CreateCategoryCommand("  Köynək  "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Köynək", result.Value.Name);
    }

    [Fact]
    public async Task Duplicate_Name_Returns_CategoryDuplicate_Error()
    {
        await using ProductsDbContext db = NewDb();
        db.Categories.Add(Category.Create("Şalvar"));
        await db.SaveChangesAsync();

        var handler = new CreateCategoryHandler(db, new CreateCategoryValidator());

        var result = await handler.Handle(new CreateCategoryCommand("Şalvar"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ProductErrors.CategoryDuplicate, result.Error);
        Assert.Equal("Bu kateqoriya artıq mövcuddur", result.Error.Message);
        Assert.Equal(1, await db.Categories.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_Name_Fails_Validation(string name)
    {
        await using ProductsDbContext db = NewDb();
        var handler = new CreateCategoryHandler(db, new CreateCategoryValidator());

        var result = await handler.Handle(new CreateCategoryCommand(name), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Kateqoriya adı boş ola bilməz", result.Error.Message);
        Assert.Equal(0, await db.Categories.CountAsync());
    }

    private static ProductsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase($"products-tests-{Guid.NewGuid()}")
            .Options;
        return new ProductsDbContext(options);
    }
}
