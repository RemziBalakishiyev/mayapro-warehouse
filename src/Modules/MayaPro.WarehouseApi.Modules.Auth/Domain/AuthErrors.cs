using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Auth.Domain;

/// <summary>
/// Business errors for the Auth module. Messages are user-facing (Azerbaijani); the frontend shows them directly.
/// </summary>
public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        new("Auth.InvalidCredentials", "Telefon və ya şifrə yanlışdır");

    public static readonly Error UserInactive =
        new("Auth.UserInactive", "Bu istifadəçi deaktiv edilib");

    public static readonly Error UserNotFound =
        new("Auth.UserNotFound", "İstifadəçi tapılmadı");
}
