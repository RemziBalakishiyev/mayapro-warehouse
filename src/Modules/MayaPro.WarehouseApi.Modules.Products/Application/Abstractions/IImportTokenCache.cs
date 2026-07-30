using MayaPro.WarehouseApi.Modules.Products.Application.Imports;

namespace MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;

/// <summary>Whether an <c>importToken</c> lookup found a live entry, a stale one, or nothing at all.</summary>
public enum ImportTokenState
{
    /// <summary>Live: the caller may commit it.</summary>
    Found,

    /// <summary>Was issued, but its TTL has passed (or it was already committed once and consumed).</summary>
    Expired,

    /// <summary>Never issued, or not a token this cache recognises.</summary>
    NotFound
}

/// <summary>
/// Server-side cache of preview parse results, keyed by <c>importToken</c> — a "simple in-memory cache" per
/// the task, not a distributed one: a single API instance is assumed. Entries live for
/// <see cref="ImportTemplate.TokenTtl"/> and are consumed (removed) on a successful commit, so a second
/// commit with the same token comes back <see cref="ImportTokenState.Expired"/>/<see cref="ImportTokenState.NotFound"/>
/// rather than re-applying the import.
/// </summary>
public interface IImportTokenCache
{
    /// <summary>Stores a fresh parse result and returns the token that claims it.</summary>
    string Store(CachedImportResult result);

    /// <summary>Looks up a token. See <see cref="ImportTokenState"/> for what each outcome means.</summary>
    (ImportTokenState State, CachedImportResult? Result) TryGet(string token);

    /// <summary>Removes a token — called once a commit has applied it, so it cannot be replayed.</summary>
    void Remove(string token);
}
