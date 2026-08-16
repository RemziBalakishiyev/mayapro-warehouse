using FluentValidation;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.ApproveTenant;

/// <summary>
/// Approves a pending shop for a named number of months: status becomes <see cref="TenantStatus.Active"/>
/// and the period starts <b>now</b> (<c>ExpiresAt = now + N ay</c>). Approval is a fresh start, so it
/// assigns the period rather than extending whatever stale date the row may carry — extending is what
/// recording a payment does.
/// </summary>
public sealed class ApproveTenantHandler(
    TenancyDbContext db,
    IDateProvider dateProvider,
    IValidator<ApproveTenantCommand> validator)
{
    public async Task<Result<TenantSummaryDto>> Handle(Guid tenantId, ApproveTenantCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<TenantSummaryDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        Tenant? tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null)
            return Result.Failure<TenantSummaryDto>(TenancyErrors.TenantNotFound);

        tenant.Approve(dateProvider.UtcNow, command.PeriodMonths);
        await db.SaveChangesAsync(ct);

        return Result.Success(TenantSummaryDto.From(tenant, dateProvider.UtcNow));
    }
}
