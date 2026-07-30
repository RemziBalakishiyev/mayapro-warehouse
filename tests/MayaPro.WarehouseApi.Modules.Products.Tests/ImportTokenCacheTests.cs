using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Imports;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Products.Tests;

/// <summary>
/// Unit tests for <see cref="ImportTokenCache"/> in isolation: a fresh token is found, an unknown one is
/// <see cref="ImportTokenState.NotFound"/>, and one whose 10-minute TTL has passed is
/// <see cref="ImportTokenState.Expired"/> — the distinction the two different 410 error codes rely on.
/// </summary>
public sealed class ImportTokenCacheTests
{
    [Fact]
    public void Store_Then_TryGet_Returns_Found_With_The_Same_Result()
    {
        var cache = new ImportTokenCache(new FakeDateProvider());
        var result = new CachedImportResult([], []);

        string token = cache.Store(result);
        (ImportTokenState state, CachedImportResult? found) = cache.TryGet(token);

        Assert.Equal(ImportTokenState.Found, state);
        Assert.Same(result, found);
    }

    [Fact]
    public void Unknown_Token_Is_NotFound()
    {
        var cache = new ImportTokenCache(new FakeDateProvider());

        (ImportTokenState state, CachedImportResult? found) = cache.TryGet("never-issued");

        Assert.Equal(ImportTokenState.NotFound, state);
        Assert.Null(found);
    }

    [Fact]
    public void Token_Past_Its_Ttl_Is_Expired_Not_NotFound()
    {
        var clock = new FakeDateProvider();
        var cache = new ImportTokenCache(clock);
        string token = cache.Store(new CachedImportResult([], []));

        clock.UtcNow = clock.UtcNow.AddMinutes(10).AddSeconds(1);

        (ImportTokenState state, CachedImportResult? found) = cache.TryGet(token);

        Assert.Equal(ImportTokenState.Expired, state);
        Assert.Null(found);
    }

    [Fact]
    public void Token_Just_Under_Its_Ttl_Is_Still_Found()
    {
        var clock = new FakeDateProvider();
        var cache = new ImportTokenCache(clock);
        string token = cache.Store(new CachedImportResult([], []));

        clock.UtcNow = clock.UtcNow.AddMinutes(9).AddSeconds(59);

        (ImportTokenState state, CachedImportResult? found) = cache.TryGet(token);

        Assert.Equal(ImportTokenState.Found, state);
        Assert.NotNull(found);
    }

    [Fact]
    public void Remove_Makes_A_Later_Lookup_NotFound()
    {
        var cache = new ImportTokenCache(new FakeDateProvider());
        string token = cache.Store(new CachedImportResult([], []));

        cache.Remove(token);

        (ImportTokenState state, CachedImportResult? found) = cache.TryGet(token);
        Assert.Equal(ImportTokenState.NotFound, state);
        Assert.Null(found);
    }

    [Fact]
    public void Two_Stores_Never_Collide_On_The_Same_Token()
    {
        var cache = new ImportTokenCache(new FakeDateProvider());
        var first = new CachedImportResult([], []);
        var second = new CachedImportResult([], []);

        string tokenA = cache.Store(first);
        string tokenB = cache.Store(second);

        Assert.NotEqual(tokenA, tokenB);
        Assert.Same(first, cache.TryGet(tokenA).Result);
        Assert.Same(second, cache.TryGet(tokenB).Result);
    }
}
