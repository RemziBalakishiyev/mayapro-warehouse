using MayaPro.WarehouseApi.Modules.Sales.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Sales.Tests;

/// <summary>
/// Hand-written test doubles for the Sales module's collaborators (the solution carries no mocking library
/// on purpose) — mirrors the pattern in the Products/Suppliers/Expenses test projects.
/// </summary>
internal sealed class FakeUnitOfWork(SalesDbContext db) : IUnitOfWork
{
    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction(db));

    private sealed class FakeTransaction(SalesDbContext db) : IUnitOfWorkTransaction
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

/// <summary>Unused by <see cref="Application.SalesModuleContract.GetDayTotalsAsync"/> — every member throws if hit.</summary>
internal sealed class UnusedProductsModule : IProductsModule
{
    public Task<Result<ProductStockSnapshot>> TryDecreaseStockAsync(
        Guid productId, int quantity, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result> IncreaseStockAsync(Guid productId, int quantity, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<ProductSnapshot>> GetSnapshotAsync(Guid productId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ProductSnapshot>> GetAllSnapshotsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result> AddExpenseToProductAsync(
        Guid productId, string category, decimal amount, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result> RemoveExpenseFromProductAsync(
        Guid productId, string category, decimal amount, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Dictionary<Guid, int>> GetCountBySupplierAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ProductExportRow>> GetExportProductsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ProductLabelInfo>> GetLabelInfoAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>Unused by <see cref="Application.SalesModuleContract.GetDayTotalsAsync"/> — every member throws if hit.</summary>
internal sealed class UnusedCustomersModule : ICustomersModule
{
    public Task<Result> IncreaseDebtAsync(Guid customerId, decimal amount, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result> DecreaseDebtAsync(Guid customerId, decimal amount, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<decimal> GetTotalDebtAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<RecentPaymentInfo>> GetRecentPaymentsAsync(
        int take, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Dictionary<Guid, string>> GetNamesAsync(
        IEnumerable<Guid> ids, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CustomerInfo?> GetCustomerInfoAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
