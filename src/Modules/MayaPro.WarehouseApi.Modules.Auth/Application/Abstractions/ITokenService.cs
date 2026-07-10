using MayaPro.WarehouseApi.Modules.Auth.Domain;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;

/// <summary>Issues signed JWT access tokens for authenticated users.</summary>
public interface ITokenService
{
    /// <summary>Creates a signed JWT with <c>sub</c>, <c>name</c> and <c>role</c> claims.</summary>
    string CreateToken(User user);
}
