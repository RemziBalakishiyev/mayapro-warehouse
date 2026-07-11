namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>The Sales module's public surface for other modules — currently day totals for day-end.</summary>
public interface ISalesModule
{
    /// <summary>Sums a day's sales by payment type (net amounts). Used by day-end closing.</summary>
    Task<SalesDayTotals> GetDayTotalsAsync(DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>A day's net sales split by payment type.</summary>
public sealed record SalesDayTotals(decimal Cash, decimal Card, decimal Nisye);
