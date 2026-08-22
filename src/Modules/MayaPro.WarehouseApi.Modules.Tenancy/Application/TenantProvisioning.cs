using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application;

/// <summary>
/// BE#36 — "create a shop and its first Sahibkar", shared by the two ways that can happen: anonymous
/// self-service registration and the platform admin creating a shop by hand. One implementation, so the two
/// paths can never disagree about the phone rule or about atomicity.
/// <para>
/// The two rows live in different modules' schemas (<c>tenancy.Tenants</c>, <c>identity.Users</c>), so the
/// write runs inside an <see cref="IUnitOfWork"/> transaction: a shop with no owner — or an owner stranded
/// in a shop that was never created — is not a state this system can reach.
/// </para>
/// <para>
/// The new user's <c>TenantId</c> is named <b>explicitly</b> by the Auth module's provisioning contract.
/// Registration is anonymous, so there is no ambient tenant for <c>TenantInterceptor</c> to stamp and it
/// would (correctly) throw <c>MissingTenantContextException</c> — the same reason every startup seeder
/// assigns its tenant by hand.
/// </para>
/// </summary>
public sealed class TenantProvisioning(
    TenancyDbContext db,
    IIdentityProvisioning identity,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Creates the shop + owner pair. <paramref name="months"/> is optional: when given, the shop is
    /// activated with a period starting now; when omitted the shop keeps <paramref name="status"/> and an
    /// open-ended (never expiring) subscription.
    /// </summary>
    public async Task<Result<Tenant>> ProvisionAsync(
        string storeName,
        string ownerName,
        string phone,
        string password,
        TenantStatus status,
        DateTime utcNow,
        int? months,
        decimal monthlyFee,
        CancellationToken ct)
    {
        // BE#46 — canonical before anything else. Both the duplicate check below and the two rows written
        // afterwards use the same 994XXXXXXXXX string, so a shop registered as "+994 50 123 45 67" and one
        // registered as "0501234567" collide here, at registration, instead of becoming two accounts that
        // then fight over the same login.
        Result<string> normalized = PhoneNormalizer.Normalize(phone);
        if (normalized.IsFailure)
            return Result.Failure<Tenant>(normalized.Error);

        string normalizedPhone = normalized.Value;

        // Global, across every shop — see IIdentityProvisioning.PhoneExistsAsync for why it has to be.
        if (await identity.PhoneExistsAsync(normalizedPhone, ct))
            return Result.Failure<Tenant>(TenancyErrors.PhoneAlreadyExists);

        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(ct);

        Tenant tenant = Tenant.Create(storeName.Trim(), ownerName.Trim(), normalizedPhone, status);
        if (months is { } period)
            tenant.Approve(utcNow, period);
        if (monthlyFee > 0)
            tenant.SetMonthlyFee(monthlyFee);

        db.Tenants.Add(tenant);
        await identity.AddOwnerAsync(tenant.Id, ownerName.Trim(), normalizedPhone, password, ct);

        await transaction.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result.Success(tenant);
    }
}
