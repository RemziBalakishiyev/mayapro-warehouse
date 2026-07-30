using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Exports.Tests;

/// <summary>
/// Hand-written doubles for the Exports handlers' collaborators (the solution carries no mocking library
/// on purpose). Only the members the label sheet actually uses are implemented; everything else throws,
/// so an accidental new dependency shows up as a failing test rather than silent behaviour.
/// </summary>
internal sealed class StubProductsModule(params ProductLabelInfo[] products) : IProductsModule
{
    private readonly Dictionary<Guid, ProductLabelInfo> _products = products.ToDictionary(p => p.Id);

    /// <summary>Every id set passed to <see cref="GetLabelInfoAsync"/>, in order — proves it is one call, deduplicated.</summary>
    public List<IReadOnlyCollection<Guid>> LabelLookups { get; } = [];

    public Task<IReadOnlyList<ProductLabelInfo>> GetLabelInfoAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        LabelLookups.Add(productIds);
        IReadOnlyList<ProductLabelInfo> found = productIds
            .Where(_products.ContainsKey)
            .Select(id => _products[id])
            .ToList();
        return Task.FromResult(found);
    }

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
}

/// <summary>A frozen clock, so the export file name is asserted against a known date.</summary>
internal sealed class FixedDateProvider(DateOnly today) : IDateProvider
{
    public DateTime UtcNow => today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    public DateOnly Today => today;

    public DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(utc);

    public DateTime ToLocalDateTime(DateTime utc) => utc;

    public (DateTime StartUtc, DateTime EndUtc) LocalDayRangeUtc(DateOnly localDate) =>
        (localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            localDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
}
