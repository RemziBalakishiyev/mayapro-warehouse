namespace MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;

/// <summary>Result of a successful login: the JWT and the user profile.</summary>
public sealed record LoginResponse(string Token, UserDto User);
