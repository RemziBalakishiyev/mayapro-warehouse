namespace MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;

/// <summary>
/// One still-unpaid debt source of one customer (BE#21). A source is either a sale that left a remaining
/// balance or the opening balance the customer was migrated in with; fully settled sources are never
/// returned. <see cref="Source"/> carries the same wire codes as the customer history feed
/// (<see cref="CustomerHistoryEntryType.Sale"/> / <see cref="CustomerHistoryEntryType.InitialDebt"/>), so
/// the frontend reads one vocabulary in both screens.
/// <para>
/// <see cref="OriginalAmount"/> is what the source added to the debt (a sale's remaining balance at sale
/// time — not its total — and the opening balance's amount), <see cref="PaidSoFar"/> is how much of it later
/// payments have consumed under the FIFO rule, and <see cref="Remaining"/> is the difference (always
/// positive here). <see cref="SourceDate"/> is the UTC instant; <see cref="DaysOld"/> counts business-zone
/// (Asia/Baku) days from it to today, so it never drifts by a UTC-vs-Baku midnight.
/// </para>
/// </summary>
public sealed record OpenDebtDto(
    Guid CustomerId,
    string CustomerName,
    string? Phone,
    string Source,
    DateTime SourceDate,
    string Description,
    decimal OriginalAmount,
    decimal PaidSoFar,
    decimal Remaining,
    int DaysOld);

/// <summary>
/// The open-debt list plus the sum every row's <see cref="OpenDebtDto.Remaining"/> adds up to, so the
/// frontend never re-adds the money itself. <see cref="TotalRemaining"/> is the total debt still owed
/// across all customers.
/// </summary>
public sealed record OpenDebtsDto(IReadOnlyList<OpenDebtDto> Items, decimal TotalRemaining);
