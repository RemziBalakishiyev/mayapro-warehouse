using FluentValidation;
using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

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
    ITenantDirectory? tenantDirectory = null)
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
    /// AC-9: only an <c>Active</c> shop may sign in. A blocked or not-yet-approved shop gets a 403 with a
    /// clear message and no token. The directory is optional so unit tests that predate multi-tenancy can
    /// still build the handler with four collaborators; in the host it is always registered.
    /// </summary>
    private async Task<Result> EnsureTenantAllowedAsync(User user, CancellationToken ct)
    {
        if (tenantDirectory is null)
            return Result.Success();

        // An unknown tenant id is treated exactly like an inactive one: same 403, same message. It should
        // not happen, and if it does we are not going to explain the difference to the caller.
        TenantInfo? tenant = await tenantDirectory.FindAsync(user.TenantId, ct);

        return tenant is { IsActive: true }
            ? Result.Success()
            : Result.Failure(AuthErrors.TenantInactiveForbidden);
    }
}
