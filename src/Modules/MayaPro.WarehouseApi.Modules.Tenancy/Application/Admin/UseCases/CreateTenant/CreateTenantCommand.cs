namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.CreateTenant;

/// <summary>
/// Body of <c>POST /api/admin/tenants</c> — the platform admin registering a shop on the customer's behalf.
/// The shop is <b>Active immediately</b> (no approval step; the admin is the approval).
/// <para>
/// <paramref name="Months"/> is optional: given, it starts a paid period now; omitted, the shop is
/// open-ended and never expires until the admin records a payment.
/// </para>
/// </summary>
public sealed record CreateTenantCommand(
    string StoreName,
    string OwnerName,
    string Phone,
    string Password,
    int? Months,
    decimal? MonthlyFee);
