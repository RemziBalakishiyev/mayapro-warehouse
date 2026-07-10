namespace MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;

/// <summary>The authenticated user, as returned by login and <c>GET /api/auth/me</c>.</summary>
public sealed record UserDto(Guid Id, string FullName, string Phone, string Role);
