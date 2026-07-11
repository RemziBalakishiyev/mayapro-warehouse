using System.Data.Common;

namespace MayaPro.WarehouseApi.SharedKernel.Application;

/// <summary>
/// A module DbContext that can take part in a shared cross-module transaction. Each participating context
/// registers itself under this contract so the <see cref="IUnitOfWork"/> can enlist all of them onto one
/// transaction without SharedKernel referencing any concrete module context.
/// </summary>
public interface ITransactionalDbContext
{
    /// <summary>Associates this context with an already-open transaction on the shared connection.</summary>
    Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Flushes this context's pending changes (within the shared transaction).</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
