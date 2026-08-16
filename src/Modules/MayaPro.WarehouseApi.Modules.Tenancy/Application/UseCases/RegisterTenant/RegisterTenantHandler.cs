using FluentValidation;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.UseCases.RegisterTenant;

/// <summary>
/// BE#36 — anonymous self-service registration: shop name + owner + phone + password create a
/// <see cref="TenantStatus.PendingApproval"/> shop and its first Sahibkar. <b>No token is issued</b> — the
/// shop cannot sign in until the platform admin approves it, and login says so
/// ("Hesabınız təsdiq gözləyir").
/// <para>
/// <b>Why the phone check is global.</b> §4.1 of <c>docs/multi-tenancy.md</c> left one open risk: since
/// BE#35 a phone is unique only inside a shop, so the same phone could be registered in two shops and login
/// — which is anonymous and has no tenant to scope by — would refuse both of them. Making registration
/// reject an already-used phone across the whole platform (409) removes that risk at its source: every
/// phone that entered through this endpoint identifies exactly one user.
/// </para>
/// <para>
/// <b>Known residual risk:</b> the check is a read followed by a write, so two simultaneous registrations of
/// the same brand-new phone can both pass it. The window is small and the outcome is the pre-existing,
/// already-handled ambiguity (both are refused at login, never a wrong shop). Closing it properly needs a
/// rate limit plus a platform-wide unique index, which Mərhələ 3 owns — see the docs.
/// </para>
/// </summary>
public sealed class RegisterTenantHandler(
    TenantProvisioning provisioning,
    IDateProvider dateProvider,
    IValidator<RegisterTenantCommand> validator)
{
    public const string PendingMessage =
        "Qeydiyyatınız qəbul edildi. Hesabınız təsdiq gözləyir";

    public async Task<Result<RegistrationResultDto>> Handle(RegisterTenantCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<RegistrationResultDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        Result<Tenant> provisioned = await provisioning.ProvisionAsync(
            command.StoreName,
            command.OwnerName,
            command.Phone,
            command.Password,
            TenantStatus.PendingApproval,
            dateProvider.UtcNow,
            months: null,
            monthlyFee: 0m,
            ct);

        if (provisioned.IsFailure)
            return Result.Failure<RegistrationResultDto>(provisioned.Error);

        Tenant tenant = provisioned.Value;
        return Result.Success(new RegistrationResultDto(
            tenant.Id, tenant.Name, tenant.Status.ToString(), PendingMessage));
    }
}
