using MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>
/// Hand-written test doubles for the Expenses handlers' collaborators (the solution carries no mocking
/// library on purpose). <see cref="RecordingProductsModule"/> is the important one: it records every call
/// into the Products real-cost chain, which is how the "a general expense never touches maya" rule is
/// proven rather than assumed.
/// </summary>
internal sealed class RecordingProductsModule : IProductsModule
{
    private readonly HashSet<Guid> _knownProducts;

    public RecordingProductsModule(params Guid[] knownProducts) => _knownProducts = [.. knownProducts];

    /// <summary>Every <c>AddExpenseToProductAsync</c> call, in order.</summary>
    public List<(Guid ProductId, string Category, decimal Amount)> Added { get; } = [];

    /// <summary>Every <c>RemoveExpenseFromProductAsync</c> call, in order.</summary>
    public List<(Guid ProductId, string Category, decimal Amount)> Removed { get; } = [];

    /// <summary>Every product read (a general expense must not even look a product up).</summary>
    public List<Guid> SnapshotsRead { get; } = [];

    public Task<Result> AddExpenseToProductAsync(
        Guid productId, string category, decimal amount, CancellationToken cancellationToken = default)
    {
        Added.Add((productId, category, amount));
        return Task.FromResult(
            _knownProducts.Contains(productId) ? Result.Success() : Result.Failure(NotFound));
    }

    public Task<Result> RemoveExpenseFromProductAsync(
        Guid productId, string category, decimal amount, CancellationToken cancellationToken = default)
    {
        Removed.Add((productId, category, amount));
        return Task.FromResult(
            _knownProducts.Contains(productId) ? Result.Success() : Result.Failure(NotFound));
    }

    public Task<Result<ProductSnapshot>> GetSnapshotAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        SnapshotsRead.Add(productId);
        return Task.FromResult(_knownProducts.Contains(productId)
            ? Result.Success(new ProductSnapshot(productId, "Test malı", "Test", 10, 1, 5m, 20m))
            : Result.Failure<ProductSnapshot>(NotFound));
    }

    private static Error NotFound => new("Products.NotFound", "Mal tapılmadı");

    public Task<Result<ProductStockSnapshot>> TryDecreaseStockAsync(
        Guid productId, int quantity, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result> IncreaseStockAsync(Guid productId, int quantity, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ProductSnapshot>> GetAllSnapshotsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Dictionary<Guid, int>> GetCountBySupplierAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ProductExportRow>> GetExportProductsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ProductLabelInfo>> GetLabelInfoAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<StockAdjustmentRow>> GetStockAdjustmentsAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>A unit of work over a single in-memory context: save flushes it, commit/rollback are no-ops.</summary>
internal sealed class FakeUnitOfWork(ExpensesDbContext db) : IUnitOfWork
{
    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction(db));

    private sealed class FakeTransaction(ExpensesDbContext db) : IUnitOfWorkTransaction
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
    public Guid? UserId { get; } = userId;

    public string? Name => "Test istifadəçi";

    public string? Role => WireFormat.Roles.Owner;

    public bool IsAuthenticated => UserId is not null;
}

/// <summary>No day is ever closed unless a date is explicitly listed.</summary>
internal sealed class FakeDayEndModule(params DateOnly[] closedDays) : IDayEndModule
{
    private readonly HashSet<DateOnly> _closed = [.. closedDays];

    public Task<ClosingSnapshot?> GetLastClosingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<ClosingSnapshot?>(null);

    public Task<bool> ClosingExistsAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(_closed.Contains(date));
}

/// <summary>UTC-as-local date provider — enough for handlers that only need "which day is this".</summary>
internal sealed class FakeDateProvider(DateTime? utcNow = null) : IDateProvider
{
    private readonly DateTime _utcNow = utcNow ?? new DateTime(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow => _utcNow;

    public DateOnly Today => DateOnly.FromDateTime(_utcNow);

    public DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(utc);

    public DateTime ToLocalDateTime(DateTime utc) => utc;

    public (DateTime StartUtc, DateTime EndUtc) LocalDayRangeUtc(DateOnly localDate) =>
        (localDate.ToDateTime(TimeOnly.MinValue), localDate.AddDays(1).ToDateTime(TimeOnly.MinValue));
}

internal static class TestDb
{
    /// <summary>
    /// An isolated in-memory Expenses context. <paramref name="tenantId"/> is the tenant its global query
    /// filter is pinned to (BE#35); the default — no tenant, i.e. <c>Guid.Empty</c> — matches the rows these
    /// tests build by hand, which carry no tenant either. Tests that exercise the startup seeder pass the
    /// default tenant, because that is the shop the seeder writes to.
    /// </summary>
    public static ExpensesDbContext New(Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"expenses-tests-{Guid.NewGuid()}")
            .Options;
        return new ExpensesDbContext(options, new FixedTenant(tenantId));
    }

    private sealed class FixedTenant(Guid? tenantId) : ICurrentTenant
    {
        public Guid? TenantId => tenantId;

        public bool HasTenant => tenantId is not null;
    }
}
