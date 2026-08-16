namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.GetTenants;

/// <summary>
/// Query string of <c>GET /api/admin/tenants</c>. Both filters are optional.
/// <paramref name="Status"/> is the <c>TenantStatus</c> name (case-insensitive);
/// <paramref name="Search"/> matches the shop name, the owner's name or the phone.
/// </summary>
public sealed record GetTenantsQuery(string? Status, string? Search);
