using FluentValidation;
using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.Login;

/// <summary>
/// Authenticates by phone + password: finds the user, checks it is active, verifies the BCrypt hash,
/// checks the shop is allowed in, then issues a JWT. Failures return Azerbaijani business errors — the same
/// "invalid credentials" message for both unknown phone and wrong password, so we don't leak which phones
/// exist.
/// <para>
/// <b>BE#35 — the one deliberate query-filter exception.</b> Login is anonymous: there is no JWT yet, hence
/// no tenant context, hence the global filter would match nothing and nobody could ever sign in. The user
/// lookup therefore runs with <see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters{TEntity}"/>.
/// Because <c>Users.Phone</c> is now unique only <i>within</i> a shop, that lookup can return several rows,
/// and the resolution is deliberately deterministic:
/// </para>
/// <list type="bullet">
///   <item>the password is verified against every candidate (always all of them — no early exit, so the
///   work does not depend on which row matched);</item>
///   <item>exactly one active candidate whose password verifies → that user signs in;</item>
///   <item>none → the usual "Telefon və ya şifrə yanlışdır";</item>
///   <item>more than one → also "Telefon və ya şifrə yanlışdır". Picking one arbitrarily would let the
///   holder of a duplicated phone+password land in a shop that is not theirs, which is exactly the leak
///   this task exists to prevent. Never a 500.</item>
/// </list>
/// <para>
/// This exception (and the ambiguity rule) is documented in <c>docs/multi-tenancy.md</c>.
/// </para>
/// </summary>
public sealed class LoginHandler(
    IAuthDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IValidator<LoginCommand> validator,
    ITenantDirectory? tenantDirectory = null,
    IDateProvider? dateProvider = null,
    IOptions<PlatformAdminOptions>? platformAdmin = null)
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<LoginResponse>(
                Error.Validation(validation.Errors[0].ErrorMessage));

        // See the type remarks: the tenant filter is bypassed here on purpose — login has no tenant yet.
        List<User> candidates = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Phone == command.Phone)
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return Result.Failure<LoginResponse>(AuthErrors.InvalidCredentials);

        List<User> matches = candidates
            .Where(u => passwordHasher.Verify(command.Password, u.PasswordHash))
            .ToList();

        if (matches.Count == 0)
        {
            // A single known phone with a wrong password keeps its old, more helpful answer: if that one
            // account is deactivated, say so. With several candidates we cannot tell which was meant, so we
            // fall back to the neutral message.
            return Result.Failure<LoginResponse>(
                candidates is [{ IsActive: false }] ? AuthErrors.UserInactive : AuthErrors.InvalidCredentials);
        }

        // Deactivated accounts never sign in; if that leaves nothing, say why (single match) or stay neutral.
        List<User> active = matches.Where(u => u.IsActive).ToList();
        if (active.Count == 0)
            return Result.Failure<LoginResponse>(AuthErrors.UserInactive);

        if (active.Count > 1)
            return Result.Failure<LoginResponse>(AuthErrors.InvalidCredentials);

        User user = active[0];

        Result tenantCheck = await EnsureTenantAllowedAsync(user, ct);
        if (tenantCheck.IsFailure)
            return Result.Failure<LoginResponse>(tenantCheck.Error);

        string token = tokenService.CreateToken(user);
        var dto = new UserDto(user.Id, user.FullName, user.Phone, user.Role.ToCode());

        return Result.Success(new LoginResponse(token, dto));
    }

    /// <summary>
    /// AC-9 (BE#35) plus the subscription gate (BE#36): only an <c>Active</c> shop whose paid period has not
    /// lapsed may sign in, and each refusal says why.
    /// <list type="table">
    ///   <item><term>PlatformAdmin</term><description>skipped entirely — the platform operator belongs to no
    ///   shop, so there is no tenant to approve or bill. Its access is guarded by the
    ///   <c>PlatformAdminOnly</c> policy on <c>/api/admin/*</c> instead.</description></item>
    ///   <item><term>PendingApproval</term><description>403 "Hesabınız təsdiq gözləyir".</description></item>
    ///   <item><term>Blocked</term><description>403 "Abunəliyiniz bitib — əlaqə: {telefon}".</description></item>
    ///   <item><term>Active but expired</term><description>403, same message, distinct code. The status is
    ///   <b>not</b> changed: <c>ExpiresAt</c> is the verdict, so recording a payment re-opens the shop with no
    ///   further bookkeeping.</description></item>
    ///   <item><term>Unknown tenant</term><description>the old generic "Mağaza aktiv deyil" — we do not
    ///   explain that state to the caller.</description></item>
    /// </list>
    /// The collaborators are optional so unit tests that predate multi-tenancy can still build the handler
    /// with four arguments; in the host they are always registered.
    /// </summary>
    private async Task<Result> EnsureTenantAllowedAsync(User user, CancellationToken ct)
    {
        if (tenantDirectory is null || user.Role == UserRole.PlatformAdmin)
            return Result.Success();

        TenantInfo? tenant = await tenantDirectory.FindAsync(user.TenantId, ct);
        if (tenant is null)
            return Result.Failure(AuthErrors.TenantInactiveForbidden);

        string? supportPhone = platformAdmin?.Value.Phone;

        if (string.Equals(tenant.Status, nameof(TenantRegistrationStatus.PendingApproval), StringComparison.Ordinal))
            return Result.Failure(AuthErrors.TenantPendingApprovalForbidden);

        if (!tenant.IsActive)
            return Result.Failure(AuthErrors.TenantBlockedForbidden(supportPhone));

        // Same check the per-request tenant gate makes, repeated here so no token is ever issued to a shop
        // that would be refused on its very next call.
        DateTime now = dateProvider?.UtcNow ?? DateTime.UtcNow;
        return tenant.IsSubscriptionExpired(now)
            ? Result.Failure(AuthErrors.SubscriptionExpiredForbidden(supportPhone))
            : Result.Success();
    }

    /// <summary>
    /// Local mirror of the Tenancy module's <c>TenantStatus</c> names — <see cref="TenantInfo.Status"/> is a
    /// string precisely so Auth does not take a dependency on another module's enum.
    /// </summary>
    private enum TenantRegistrationStatus
    {
        PendingApproval,
        Active,
        Blocked
    }
}
