namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.UseCases.RegisterTenant;

/// <summary>Body of the anonymous <c>POST /api/auth/register</c>.</summary>
public sealed record RegisterTenantCommand(
    string StoreName,
    string OwnerName,
    string Phone,
    string Password);
