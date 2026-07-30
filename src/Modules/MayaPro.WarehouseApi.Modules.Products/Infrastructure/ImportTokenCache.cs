using System.Collections.Concurrent;
using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Imports;
using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Products.Infrastructure;

/// <summary>
/// A process-local <see cref="IImportTokenCache"/> — a <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// keyed by token, each entry carrying its own absolute expiry. Registered as a singleton so a token
/// survives across the two different HTTP requests (preview, then commit) that share it.
/// <para>
/// Expiry is lazy (checked on <see cref="TryGet"/>, not by a background sweep) and driven by
/// <see cref="IDateProvider"/> rather than the wall clock directly, so tests can simulate "10 minutes
/// later" without an actual wait. An expired entry is still physically present until the next lookup
/// removes it — that lookup is what tells <see cref="ImportTokenState.Expired"/> apart from
/// <see cref="ImportTokenState.NotFound"/> (a token that was never issued at all).
/// </para>
/// </summary>
public sealed class ImportTokenCache(IDateProvider dateProvider) : IImportTokenCache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public string Store(CachedImportResult result)
    {
        string token = Guid.NewGuid().ToString("N");
        _entries[token] = new Entry(result, dateProvider.UtcNow + ImportTemplate.TokenTtl);
        return token;
    }

    public (ImportTokenState State, CachedImportResult? Result) TryGet(string token)
    {
        if (!_entries.TryGetValue(token, out Entry entry))
            return (ImportTokenState.NotFound, null);

        if (entry.ExpiresAtUtc <= dateProvider.UtcNow)
        {
            _entries.TryRemove(token, out _);
            return (ImportTokenState.Expired, null);
        }

        return (ImportTokenState.Found, entry.Result);
    }

    public void Remove(string token) => _entries.TryRemove(token, out _);

    private readonly record struct Entry(CachedImportResult Result, DateTime ExpiresAtUtc);
}
