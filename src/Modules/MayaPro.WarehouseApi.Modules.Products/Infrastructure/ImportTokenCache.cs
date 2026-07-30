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
/// Expiry is driven by <see cref="IDateProvider"/> rather than the wall clock directly, so tests can
/// simulate "10 minutes later" without waiting. Because previews are far more common than commits (a user
/// may upload a file and simply walk away), every <see cref="Store"/> first sweeps the entries that have
/// expired: without that, abandoned previews — up to 1000 parsed rows each — would pile up for the lifetime
/// of the process. The dictionary is small (one entry per in-flight import), so the sweep is cheap.
/// </para>
/// </summary>
public sealed class ImportTokenCache(IDateProvider dateProvider) : IImportTokenCache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public string Store(CachedImportResult result)
    {
        RemoveExpired();

        string token = Guid.NewGuid().ToString("N");
        _entries[token] = new Entry(result, dateProvider.UtcNow + ImportTemplate.TokenTtl);
        return token;
    }

    public (ImportTokenState State, CachedImportResult? Result) Claim(string token)
    {
        // TryRemove is the atomic step: whoever removes the entry owns the import, so two concurrent
        // commits with the same token can never both apply it.
        if (!_entries.TryRemove(token, out Entry entry))
            return (ImportTokenState.NotFound, null);

        if (entry.ExpiresAtUtc <= dateProvider.UtcNow)
            return (ImportTokenState.Expired, null);

        return (ImportTokenState.Found, entry.Result);
    }

    public void Restore(string token, CachedImportResult result) =>
        _entries[token] = new Entry(result, dateProvider.UtcNow + ImportTemplate.TokenTtl);

    private void RemoveExpired()
    {
        DateTime now = dateProvider.UtcNow;
        foreach (KeyValuePair<string, Entry> pair in _entries)
        {
            if (pair.Value.ExpiresAtUtc <= now)
                _entries.TryRemove(pair.Key, out _);
        }
    }

    private readonly record struct Entry(CachedImportResult Result, DateTime ExpiresAtUtc);
}
