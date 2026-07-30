using MayaPro.WarehouseApi.Modules.Products.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Products.Tests;

/// <summary>
/// Hand-written test doubles for the Products handlers' collaborators (the solution carries no mocking
/// library on purpose) — mirrors the pattern in the Expenses/Suppliers test projects.
/// </summary>
internal sealed class FakeUnitOfWork(ProductsDbContext db) : IUnitOfWork
{
    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction(db));

    private sealed class FakeTransaction(ProductsDbContext db) : IUnitOfWorkTransaction
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            db.SaveChangesAsync(cancellationToken);

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class FakeActivityLogger : IActivityLogger
{
    public List<(string Type, string Message)> Entries { get; } = [];

    public Task LogAsync(string type, string message, Guid? userId, CancellationToken cancellationToken = default)
    {
        Entries.Add((type, message));
        return Task.CompletedTask;
    }
}

internal sealed class FakeCurrentUser(Guid? userId = null) : ICurrentUser
{
    public Guid? UserId { get; } = userId ?? Guid.NewGuid();

    public string? Name => "Test istifadəçi";

    public string? Role => "Owner";

    public bool IsAuthenticated => true;
}

/// <summary>A controllable clock — lets import-token TTL tests fast-forward without a real 10-minute wait.</summary>
internal sealed class FakeDateProvider(DateTime? utcNow = null) : IDateProvider
{
    public DateTime UtcNow { get; set; } = utcNow ?? new DateTime(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc);

    public DateOnly Today => DateOnly.FromDateTime(UtcNow);

    public DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(utc);

    public DateTime ToLocalDateTime(DateTime utc) => utc;

    public (DateTime StartUtc, DateTime EndUtc) LocalDayRangeUtc(DateOnly localDate) =>
        (localDate.ToDateTime(TimeOnly.MinValue), localDate.AddDays(1).ToDateTime(TimeOnly.MinValue));
}
