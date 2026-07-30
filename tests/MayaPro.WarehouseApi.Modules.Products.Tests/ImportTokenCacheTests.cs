using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Imports;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Products.Tests;

/// <summary>
/// Unit tests for <see cref="ImportTokenCache"/> in isolation: claiming is one-shot and atomic, an unknown
/// token is <see cref="ImportTokenState.NotFound"/>, one past its 10-minute TTL is
/// <see cref="ImportTokenState.Expired"/> (the distinction the two 410 error codes rely on), and abandoned
/// previews do not accumulate.
/// </summary>
public sealed class ImportTokenCacheTests
{
    [Fact]
    public void Store_Then_Claim_Returns_Found_With_The_Same_Result()
    {
        var cache = new ImportTokenCache(new FakeDateProvider());
        CachedImportResult result = EmptyResult();

        string token = cache.Store(result);
        (ImportTokenState state, CachedImportResult? claimed) = cache.Claim(token);

        Assert.Equal(ImportTokenState.Found, state);
        Assert.Same(result, claimed);
    }

    [Fact]
    public void A_Token_Can_Only_Be_Claimed_Once()
    {
        var cache = new ImportTokenCache(new FakeDateProvider());
        string token = cache.Store(EmptyResult());

        Assert.Equal(ImportTokenState.Found, cache.Claim(token).State);
        Assert.Equal(ImportTokenState.NotFound, cache.Claim(token).State);
    }

    [Fact]
    public void Parallel_Claims_Of_One_Token_Hand_The_Result_To_Exactly_One_Caller()
    {
        var cache = new ImportTokenCache(new FakeDateProvider());
        string token = cache.Store(EmptyResult());

        (ImportTokenState State, CachedImportResult? Result)[] outcomes = Enumerable.Range(0, 32)
            .AsParallel()
            .Select(_ => cache.Claim(token))
            .ToArray();

        Assert.Single(outcomes, o => o.State == ImportTokenState.Found);
    }

    [Fact]
    public void Unknown_Token_Is_NotFound()
    {
        var cache = new ImportTokenCache(new FakeDateProvider());

        (ImportTokenState state, CachedImportResult? found) = cache.Claim("never-issued");

        Assert.Equal(ImportTokenState.NotFound, state);
        Assert.Null(found);
    }

    [Fact]
    public void Token_Past_Its_Ttl_Is_Expired_Not_NotFound()
    {
        var clock = new FakeDateProvider();
        var cache = new ImportTokenCache(clock);
        string token = cache.Store(EmptyResult());

        clock.UtcNow = clock.UtcNow.AddMinutes(10).AddSeconds(1);

        (ImportTokenState state, CachedImportResult? found) = cache.Claim(token);

        Assert.Equal(ImportTokenState.Expired, state);
        Assert.Null(found);
    }

    [Fact]
    public void Token_Just_Under_Its_Ttl_Is_Still_Claimable()
    {
        var clock = new FakeDateProvider();
        var cache = new ImportTokenCache(clock);
        string token = cache.Store(EmptyResult());

        clock.UtcNow = clock.UtcNow.AddMinutes(9).AddSeconds(59);

        (ImportTokenState state, CachedImportResult? found) = cache.Claim(token);

        Assert.Equal(ImportTokenState.Found, state);
        Assert.NotNull(found);
    }

    [Fact]
    public void Restore_Makes_A_Claimed_Token_Usable_Again()
    {
        var cache = new ImportTokenCache(new FakeDateProvider());
        CachedImportResult result = EmptyResult();
        string token = cache.Store(result);

        cache.Claim(token);
        cache.Restore(token, result);

        (ImportTokenState state, CachedImportResult? found) = cache.Claim(token);
        Assert.Equal(ImportTokenState.Found, state);
        Assert.Same(result, found);
    }

    [Fact]
    public void Abandoned_Previews_Are_Swept_Instead_Of_Piling_Up_Forever()
    {
        var clock = new FakeDateProvider();
        var cache = new ImportTokenCache(clock);
        string abandoned = cache.Store(EmptyResult());

        // Nobody ever commits it; 11 minutes later another preview comes in.
        clock.UtcNow = clock.UtcNow.AddMinutes(11);
        cache.Store(EmptyResult());

        // Swept on the way in — gone entirely, not merely reported as expired.
        Assert.Equal(ImportTokenState.NotFound, cache.Claim(abandoned).State);
    }

    [Fact]
    public void Two_Stores_Never_Collide_On_The_Same_Token()
    {
        var cache = new ImportTokenCache(new FakeDateProvider());
        CachedImportResult first = EmptyResult();
        CachedImportResult second = EmptyResult();

        string tokenA = cache.Store(first);
        string tokenB = cache.Store(second);

        Assert.NotEqual(tokenA, tokenB);
        Assert.Same(first, cache.Claim(tokenA).Result);
        Assert.Same(second, cache.Claim(tokenB).Result);
    }

    private static CachedImportResult EmptyResult() => new([], [], Guid.NewGuid());
}
