using System.Data;
using System.Data.Common;
using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.SharedKernel.Infrastructure;

/// <summary>
/// Default <see cref="IUnitOfWork"/>: begins one transaction on the scope's shared connection and enlists
/// every registered <see cref="ITransactionalDbContext"/>. Contexts not touched by the current chain
/// enlist harmlessly and simply save zero rows.
/// </summary>
public sealed class UnitOfWork(
    IDbConnectionFactory connectionFactory,
    IEnumerable<ITransactionalDbContext> contexts) : IUnitOfWork
{
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        DbConnection connection = connectionFactory.GetConnection();
        bool openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(cancellationToken);

        DbTransaction? transaction = null;
        try
        {
            transaction = await connection.BeginTransactionAsync(cancellationToken);

            var enlisted = contexts.ToList();
            foreach (ITransactionalDbContext context in enlisted)
                await context.EnlistAsync(transaction, cancellationToken);

            return new UnitOfWorkTransaction(transaction, enlisted);
        }
        catch
        {
            // Something failed between opening the connection and handing back a disposable wrapper — the
            // caller never gets a transaction to dispose, so unwind here: kill the dangling transaction and,
            // if we were the ones who opened the connection, hand it straight back to the pool. Otherwise a
            // transient begin/enlist failure would pin this pooled connection until the scope ends.
            if (transaction is not null)
                await transaction.DisposeAsync();
            if (openedHere && connection.State == ConnectionState.Open)
                await connection.CloseAsync();
            throw;
        }
    }

    private sealed class UnitOfWorkTransaction(
        DbTransaction transaction,
        IReadOnlyList<ITransactionalDbContext> contexts) : IUnitOfWorkTransaction
    {
        private bool _committed;

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            int total = 0;
            foreach (ITransactionalDbContext context in contexts)
                total += await context.SaveChangesAsync(cancellationToken);

            return total;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default) =>
            transaction.RollbackAsync(cancellationToken);

        public async ValueTask DisposeAsync()
        {
            // Roll back anything that reached the transaction but was never committed (early return / throw).
            if (!_committed)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // The transaction may already be gone (e.g. connection dropped); nothing to undo.
                }
            }

            await transaction.DisposeAsync();
        }
    }
}
