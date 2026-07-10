namespace MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;

/// <summary>An employee row for <c>GET /api/employees</c>.</summary>
public sealed record EmployeeDto(Guid Id, string FullName, string Phone, string Role, bool IsActive);
