namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>The DayEnd module's public surface for other modules — currently the most recent closing.</summary>
public interface IDayEndModule
{
    /// <summary>
    /// Returns the most recent day-end closing, or <c>null</c> if the day has never been closed. Used by
    /// the read-only Reports module to anchor "expected cash in the drawer" to the last close.
    /// </summary>
    Task<ClosingSnapshot?> GetLastClosingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the given business day has already been closed. Used by the Sales and Expenses modules to
    /// forbid editing or deleting a record whose day is locked — once a day's books are closed, its sales
    /// and expenses must not change.
    /// </summary>
    Task<bool> ClosingExistsAsync(DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>The bits of a closing other modules need: its date and the cash counted at close.</summary>
public sealed record ClosingSnapshot(DateOnly Date, decimal ActualCash);
