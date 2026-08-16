using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Domain;

/// <summary>
/// BE#36 — one subscription payment a shop made to the platform, recorded by the platform admin.
/// <para>
/// Deliberately <b>not</b> <c>ITenantScoped</c>, for the same reason <see cref="Tenant"/> is not: this is a
/// platform-level billing record, not shop data. A shop never reads it (there is no shop-facing billing
/// endpoint), and the only reader — the admin — has no tenant context at all, so a query filter here would
/// only mean "the admin sees nothing" and would force an <c>IgnoreQueryFilters()</c> bypass into the very
/// module that is supposed to have none. <see cref="TenantId"/> is therefore a plain Guid column pointing at
/// <c>tenancy.Tenants</c>, exactly like every other cross-entity link in this codebase.
/// </para>
/// </summary>
public sealed class SubscriptionPayment : Entity
{
    // EF Core constructor.
    private SubscriptionPayment() { }

    private SubscriptionPayment(
        Guid tenantId,
        decimal amount,
        DateTime paidAt,
        int periodMonths,
        string? note,
        Guid? recordedByAdminId)
    {
        TenantId = tenantId;
        Amount = amount;
        PaidAt = paidAt;
        PeriodMonths = periodMonths;
        Note = note;
        RecordedByAdminId = recordedByAdminId;
    }

    /// <summary>The shop that paid. A bare id — no navigation into another module.</summary>
    public Guid TenantId { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>When the money was received, in UTC.</summary>
    public DateTime PaidAt { get; private set; }

    /// <summary>How many months this payment bought — the same number <c>Tenant.Extend</c> was given.</summary>
    public int PeriodMonths { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The platform admin who entered the record. Null only for rows written by tooling.</summary>
    public Guid? RecordedByAdminId { get; private set; }

    public static SubscriptionPayment Create(
        Guid tenantId,
        decimal amount,
        DateTime paidAtUtc,
        int periodMonths,
        string? note,
        Guid? recordedByAdminId) =>
        new(tenantId, amount, paidAtUtc, periodMonths, note, recordedByAdminId);
}
