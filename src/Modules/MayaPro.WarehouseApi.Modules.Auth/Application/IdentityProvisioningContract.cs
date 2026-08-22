using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application;

/// <summary>
/// The Auth module's implementation of <see cref="IIdentityProvisioning"/> — how the Tenancy module gets a
/// shop's first Sahibkar created without touching the <c>identity</c> schema itself.
/// <para>
/// <b>BE#36 — a documented query-filter exception.</b> <see cref="PhoneExistsAsync"/> runs with
/// <see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters{TEntity}"/> because registration is
/// anonymous: there is no tenant context, so the filter would match nothing and every phone would look free.
/// The bypass is as narrow as it can be — it returns a <c>bool</c> from an <c>Any()</c>, reads no row and
/// exposes no field, so nothing about another shop can leak through it. It is also what makes the rule
/// correct: the question really is "is this phone taken <i>anywhere</i>?" (docs §4.1).
/// </para>
/// <para>
/// <see cref="AddOwnerAsync"/> names the tenant explicitly — registration has no ambient tenant, so
/// <c>TenantInterceptor</c> would rightly refuse to stamp the row — and does <b>not</b> save: the caller owns
/// the transaction that also writes the <c>tenancy.Tenants</c> row.
/// </para>
/// </summary>
internal sealed class IdentityProvisioningContract(IAuthDbContext db, IPasswordHasher passwordHasher)
    : IIdentityProvisioning
{
    public async Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken = default)
    {
        // BE#46 — ask the question in the canonical form the column is stored in, otherwise "+994 50 123 45 67"
        // would look free while "994501234567" already owns that login. Callers normalize first; this repeats
        // it because the contract is public and "is this phone taken?" must not depend on who asks. An input
        // that cannot be canonicalized is compared verbatim: it can match no stored row, which is the truthful
        // answer, and refusing it is the caller's job (they return the format error before ever getting here).
        Result<string> canonical = PhoneNormalizer.Normalize(phone);
        string normalized = canonical.IsSuccess ? canonical.Value : phone.Trim();

        return await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Phone == normalized, cancellationToken);
    }

    public Task<Guid> AddOwnerAsync(
        Guid tenantId,
        string fullName,
        string phone,
        string password,
        CancellationToken cancellationToken = default)
    {
        // BE#46 — the login identifier goes in canonically or not at all. The caller already normalized, so
        // the fallback is unreachable in practice; keeping it means a future caller cannot silently write a
        // row that login would then never find.
        Result<string> canonical = PhoneNormalizer.Normalize(phone);
        string normalizedPhone = canonical.IsSuccess ? canonical.Value : phone.Trim();

        User owner = User.Create(fullName, normalizedPhone, null, passwordHasher.Hash(password), UserRole.Owner);
        owner.AssignTenant(tenantId);

        db.Users.Add(owner);

        return Task.FromResult(owner.Id);
    }
}
