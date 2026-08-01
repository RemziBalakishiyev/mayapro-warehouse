namespace MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;

/// <summary>
/// BE#27 — <c>GET /api/reports/debts-kpi?from=&amp;to=</c>. <see cref="TotalOutstanding"/>,
/// <see cref="DebtorCount"/> and <see cref="TopDebtor"/> are "as of now" (never move with
/// <c>from</c>/<c>to</c>) — debt is a running balance, not a period total. <see cref="PeriodNewDebt"/> and
/// <see cref="PeriodCollected"/> are the only period-scoped fields.
/// <para>
/// <see cref="OldestDebtDays"/> is computed from the oldest still-unpaid sale
/// (<c>ISalesModule.GetOutstandingSalesAsync</c>) and does not see debt carried in as a migrated opening
/// balance (<c>CustomerDebtAdjustment</c>) — a known limitation shared with the open-debts (BE#21) list,
/// documented rather than silently wrong.
/// </para>
/// </summary>
public sealed record DebtsKpiDto(
    decimal TotalOutstanding,
    int DebtorCount,
    TopDebtorDto? TopDebtor,
    decimal PeriodNewDebt,
    decimal PeriodCollected,
    int? OldestDebtDays);

/// <summary>The customer with the largest outstanding balance, and how much they owe.</summary>
public sealed record TopDebtorDto(string Name, decimal Amount);
