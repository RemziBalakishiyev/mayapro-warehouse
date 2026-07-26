using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Tests;

/// <summary>
/// Records every call to <see cref="IActivityLogger.LogAsync"/> so tests can assert on the type/message a
/// handler wrote, without a mocking library.
/// </summary>
internal sealed class FakeActivityLogger : IActivityLogger
{
    public List<(string Type, string Message)> Entries { get; } = [];

    public Task LogAsync(string type, string message, Guid? userId, CancellationToken cancellationToken = default)
    {
        Entries.Add((type, message));
        return Task.CompletedTask;
    }
}

/// <summary>A fixed authenticated user for handler unit tests.</summary>
internal sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
{
    public Guid? UserId { get; } = userId;

    public string? Name => "Test User";

    public string? Role => "Owner";

    public bool IsAuthenticated => true;
}

/// <summary>
/// A single-context stand-in for the real cross-module <see cref="IUnitOfWork"/>: the InMemory provider has
/// no real transactions, so this simply flushes the one DbContext under test and no-ops commit/rollback.
/// </summary>
internal sealed class FakeUnitOfWork(DbContext db) : IUnitOfWork
{
    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction(db));

    private sealed class FakeTransaction(DbContext db) : IUnitOfWorkTransaction
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            db.SaveChangesAsync(cancellationToken);

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
