namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>The Expenses module's public surface for other modules — currently a day total for day-end.</summary>
public interface IExpensesModule
{
    /// <summary>Sums a day's expenses. Used by day-end closing.</summary>
    Task<decimal> GetDayTotalAsync(DateOnly date, CancellationToken cancellationToken = default);
}
