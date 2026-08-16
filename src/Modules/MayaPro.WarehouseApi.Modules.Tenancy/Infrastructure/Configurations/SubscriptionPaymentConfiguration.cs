using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure.Configurations;

public sealed class SubscriptionPaymentConfiguration : IEntityTypeConfiguration<SubscriptionPayment>
{
    public void Configure(EntityTypeBuilder<SubscriptionPayment> builder)
    {
        builder.ToTable("SubscriptionPayments");

        builder.HasKey(p => p.Id);

        // A bare Guid, not an FK/navigation — the same rule every cross-entity link in this codebase follows.
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.Amount).IsRequired();
        builder.Property(p => p.PaidAt).IsRequired();
        builder.Property(p => p.PeriodMonths).IsRequired();
        builder.Property(p => p.Note).HasMaxLength(500);

        // "This shop's payments, newest first" and "this month's takings" are the only two reads.
        builder.HasIndex(p => new { p.TenantId, p.PaidAt });
        builder.HasIndex(p => p.PaidAt);
    }
}
