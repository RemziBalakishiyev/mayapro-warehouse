using MayaPro.WarehouseApi.Modules.Products.Application.Imports;

namespace MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;

/// <summary>Whether an <c>importToken</c> lookup found a live entry, a stale one, or nothing at all.</summary>
public enum ImportTokenState
{
    /// <summary>Live: the caller may commit it.</summary>
    Found,

    /// <summary>Was issued, but its TTL has passed.</summary>
    Expired,

    /// <summary>Never issued, already committed, or not a token this cache recognises.</summary>
    NotFound
}

/// <summary>
/// Server-side cache of preview parse results, keyed by <c>importToken</c> — a "simple in-memory cache" per
/// the task, not a distributed one: a single API instance is assumed. Entries live for
/// <see cref="ImportTemplate.TokenTtl"/>.
/// <para>
/// A commit <see cref="Claim"/>s its token, which hands the result over and removes it in one atomic step:
/// two commits racing on the same token cannot both apply the import, and a replayed token comes back
/// <see cref="ImportTokenState.NotFound"/>. <see cref="Restore"/> exists only for the failure path — when a
/// commit transaction rolls back, the entry goes back so the user can retry without re-uploading the file.
/// </para>
/// </summary>
public interface IImportTokenCache
{
    /// <summary>Stores a fresh parse result and returns the token that claims it.</summary>
    string Store(CachedImportResult result);

    /// <summary>
    /// Atomically takes the entry out of the cache. See <see cref="ImportTokenState"/> for the outcomes; the
    /// result is non-null only for <see cref="ImportTokenState.Found"/>.
    /// </summary>
    (ImportTokenState State, CachedImportResult? Result) Claim(string token);

    /// <summary>Puts a claimed entry back (same token, fresh TTL) after a commit failed to apply it.</summary>
    void Restore(string token, CachedImportResult result);
}
