using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.Extensions.Logging;

namespace MayaPro.WarehouseApi.Modules.Customers.Tests;

/// <summary>
/// Serves a fixed set of still-owing sales to the open-debt handler. Every other member of the contract
/// throws, so an accidental new dependency shows up as a failing test rather than silent empty data.
/// </summary>
internal sealed class FakeSalesModule(params CustomerOutstandingSale[] outstanding) : ISalesModule
{
    public Task<IReadOnlyList<CustomerOutstandingSale>> GetOutstandingSalesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CustomerOutstandingSale>>(outstanding);

    public Task<SalesDayTotals> GetDayTotalsAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<SalesReportRow>> GetSalesAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ProductLastSale>> GetLastSaleDatesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<RecentSaleInfo>> GetRecentSalesAsync(int take, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<CustomerPurchaseStats>> GetPurchaseStatsByCustomerAsync(
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<CustomerSaleEntry>> GetSalesByCustomerAsync(
        Guid customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Result> DeleteCreditSaleAsync(
        Guid saleId, Guid customerId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SaleInvoiceInfo?> GetInvoiceSaleAsync(Guid saleId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Guid?> GetSaleIdByInvoiceTokenAsync(string token, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>A frozen clock for handler unit tests — "today" and the local date are fixed and UTC-based.</summary>
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

/// <summary>Captures the log entries a handler wrote so tests can assert on warnings without a mocking library.</summary>
internal sealed class FakeLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}
