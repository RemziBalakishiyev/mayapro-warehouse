namespace MayaPro.WarehouseApi.SharedKernel.Application;

/// <summary>
/// The authenticated caller, resolved from the JWT on the current request. Implemented in the API host
/// over <c>HttpContext</c>. Other modules depend on this (e.g. activity logging) without touching HTTP.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The user id (JWT <c>sub</c> claim), or <c>null</c> for anonymous requests.</summary>
    Guid? UserId { get; }

    /// <summary>The user's full name (JWT <c>name</c> claim), or <c>null</c> for anonymous requests.</summary>
    string? Name { get; }

    /// <summary>The role name (JWT <c>role</c> claim), or <c>null</c> for anonymous requests.</summary>
    string? Role { get; }

    /// <summary>True when the request carries a valid authenticated identity.</summary>
    bool IsAuthenticated { get; }
}
