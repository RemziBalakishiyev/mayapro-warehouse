namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>
/// BE#36 — the Auth module's provisioning surface, used by the Tenancy module when a shop is created (either
/// by self-service registration or by the platform admin). Tenancy owns the <c>tenancy</c> schema and Auth
/// owns <c>identity</c>; neither reads the other's tables, so the first Sahibkar user is created through
/// this contract.
/// <para>
/// Like every other cross-module contract, <see cref="AddOwnerAsync"/> does <b>not</b> save — the caller
/// owns the transaction, so the tenant row and its owner user commit together or not at all.
/// </para>
/// </summary>
public interface IIdentityProvisioning
{
    /// <summary>
    /// Is this phone already a login anywhere on the platform? Deliberately <b>global</b> (across all
    /// shops): registration is anonymous, so there is no tenant to scope the question to, and a globally
    /// unique registration phone is what keeps the anonymous login unambiguous — see
    /// <c>docs/multi-tenancy.md</c> §4.1.
    /// </summary>
    Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the shop's first <c>Owner</c> (Sahibkar) user to the change tracker with its tenant named
    /// explicitly, and returns the new user's id. Not saved — the caller commits.
    /// </summary>
    Task<Guid> AddOwnerAsync(
        Guid tenantId,
        string fullName,
        string phone,
        string password,
        CancellationToken cancellationToken = default);
}
