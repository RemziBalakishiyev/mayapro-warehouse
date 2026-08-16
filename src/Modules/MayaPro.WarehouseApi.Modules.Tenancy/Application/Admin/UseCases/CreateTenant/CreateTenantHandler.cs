using FluentValidation;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.CreateTenant;

/// <summary>
/// The platform admin creates a shop directly: same provisioning as self-service registration, but the shop
/// starts <see cref="TenantStatus.Active"/> — the admin's action <i>is</i> the approval. The duplicate-phone
/// rule is the shared one (409), so the admin cannot create a shop whose owner phone already logs in
/// somewhere else.
/// </summary>
public sealed class CreateTenantHandler(
    TenantProvisioning provisioning,
    IDateProvider dateProvider,
    IValidator<CreateTenantCommand> validator)
{
    public async Task<Result<TenantListItemDto>> Handle(CreateTenantCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<TenantListItemDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        DateTime now = dateProvider.UtcNow;

        Result<Tenant> provisioned = await provisioning.ProvisionAsync(
            command.StoreName,
            command.OwnerName,
            command.Phone,
            command.Password,
            TenantStatus.Active,
            now,
            command.Months,
            command.MonthlyFee ?? 0m,
            ct);

        if (provisioned.IsFailure)
            return Result.Failure<TenantListItemDto>(provisioned.Error);

        Tenant tenant = provisioned.Value;

        // Brand new shop: no payments yet, so the billing columns are empty by construction.
        return Result.Success(new TenantListItemDto(
            tenant.Id,
            tenant.Name,
            tenant.OwnerName,
            tenant.Phone,
            tenant.Status.ToString(),
            tenant.ExpiresAt,
            tenant.MonthlyFee,
            tenant.IsSubscriptionExpired(now),
            LastPaymentAt: null,
            LastPaymentAmount: null,
            TotalPaid: 0m));
    }
}
