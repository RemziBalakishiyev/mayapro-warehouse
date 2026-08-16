using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

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

    /// <summary>
    /// BE#35 — the credentials were fine but the shop is not allowed in (unknown, or in a state with no
    /// more specific message). The <c>Forbidden</c> suffix maps this to HTTP 403 and no token is issued.
    /// <para>
    /// BE#36 split the two states a customer actually reaches out of this one:
    /// <see cref="TenantPendingApprovalForbidden"/> and <see cref="TenantBlockedForbidden"/>. This error
    /// remains the fallback for a token naming a shop that does not exist — a state no honest client is in,
    /// and one we deliberately do not explain.
    /// </para>
    /// </summary>
    public static readonly Error TenantInactiveForbidden =
        new("Auth.TenantInactiveForbidden", "Mağaza aktiv deyil");

    /// <summary>BE#36 — registered, but the platform admin has not approved the shop yet.</summary>
    public static readonly Error TenantPendingApprovalForbidden =
        new("Auth.TenantPendingApprovalForbidden", "Hesabınız təsdiq gözləyir");

    /// <summary>
    /// BE#36 — the shop was blocked by support. The message carries the platform's contact number so the
    /// customer knows who to call; it is configuration (<c>PlatformAdmin:Phone</c>), never hard-coded.
    /// </summary>
    public static Error TenantBlockedForbidden(string? supportPhone) =>
        new("Auth.TenantBlockedForbidden", SubscriptionMessage(supportPhone));

    /// <summary>
    /// BE#36 — the shop is <c>Active</c> but its paid period has lapsed. Same wording as a block, because
    /// from the customer's side it is the same situation and the same phone call fixes it; the distinct code
    /// is what lets the frontend (and support) tell them apart.
    /// <para>
    /// BE#41 — the code is the bare <c>SubscriptionExpired</c>, not <c>Auth.SubscriptionExpiredForbidden</c>:
    /// it is a frozen contract string the frontend hard-codes, shared verbatim with the per-request gate in
    /// <c>TenantGateMiddleware</c>, and therefore lives in <see cref="WireFormat.ErrorCodes"/>. Because it
    /// carries no <c>Forbidden</c> suffix, its 403 comes from an explicit rule in
    /// <c>ResultExtensions.StatusCodeFor</c>.
    /// </para>
    /// </summary>
    public static Error SubscriptionExpired(string? supportPhone) =>
        new(WireFormat.ErrorCodes.SubscriptionExpired, SubscriptionMessage(supportPhone));

    /// <summary>
    /// "Abunəliyiniz bitib — əlaqə: {telefon}", degrading gracefully when no contact number is configured
    /// (the message must still be a sentence, not a dangling dash).
    /// </summary>
    public static string SubscriptionMessage(string? supportPhone) =>
        string.IsNullOrWhiteSpace(supportPhone)
            ? "Abunəliyiniz bitib — dəstək ilə əlaqə saxlayın"
            : $"Abunəliyiniz bitib — əlaqə: {supportPhone}";
}
