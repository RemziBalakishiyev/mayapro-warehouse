using FluentValidation;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.RecordPayment;

/// <summary>
/// Records a subscription payment and extends the shop's paid period in the same save:
/// <c>ExpiresAt = max(now, mövcud ExpiresAt ?? now) + N ay</c> (see <see cref="Tenant.Extend"/> for why).
/// <para>
/// This is also the <b>unblock-by-payment</b> path the auto-block design relies on: expiry never changes the
/// status, so pushing <c>ExpiresAt</c> into the future is all it takes for the shop to work again on its
/// very next request. A shop that support has explicitly <see cref="TenantStatus.Blocked"/> stays blocked —
/// paying does not overrule a support decision.
/// </para>
/// </summary>
public sealed class RecordPaymentHandler(
    TenancyDbContext db,
    IDateProvider dateProvider,
    ICurrentUser currentUser,
    IValidator<RecordPaymentCommand> validator)
{
    public async Task<Result<TenantSummaryDto>> Handle(
        Guid tenantId,
        RecordPaymentCommand command,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<TenantSummaryDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        Tenant? tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null)
            return Result.Failure<TenantSummaryDto>(TenancyErrors.TenantNotFound);

        DateTime now = dateProvider.UtcNow;

        db.SubscriptionPayments.Add(SubscriptionPayment.Create(
            tenant.Id,
            command.Amount,
            now,
            command.Months,
            string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim(),
            currentUser.UserId));

        tenant.Extend(now, command.Months);

        // One SaveChanges: the payment row and the new expiry are the same fact, recorded together.
        await db.SaveChangesAsync(ct);

        return Result.Success(TenantSummaryDto.From(tenant, now));
    }
}
