namespace MayaPro.WarehouseApi.SharedKernel.Application;

/// <summary>
/// Coordinates a single transaction spanning every participating module DbContext in the scope. Because
/// they all share one connection (see <see cref="IDbConnectionFactory"/>), a business chain that touches
/// several modules (sale → stock → debt) commits or rolls back atomically — no distributed transaction.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Opens the shared connection (if needed), begins a transaction on it, and enlists every
    /// participating context. Dispose without committing to roll back.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A running cross-module transaction. Save flushes all enlisted contexts; commit finalises; disposing
/// before a commit rolls everything back.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    /// <summary>Saves pending changes across every enlisted context. Returns total rows affected.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
