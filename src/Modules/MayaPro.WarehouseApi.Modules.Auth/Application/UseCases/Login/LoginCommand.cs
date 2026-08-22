namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.Login;

/// <summary>
/// BE#45 — <paramref name="RememberMe"/> is optional (defaults to <c>false</c>) so existing clients that
/// omit it keep the normal <c>Jwt:ExpiryHours</c> token lifetime; <c>true</c> issues a long-lived token
/// instead (<c>Jwt:RememberMeExpiryDays</c>). The response shape is unchanged either way.
/// </summary>
public sealed record LoginCommand(string Phone, string Password, bool RememberMe = false);
