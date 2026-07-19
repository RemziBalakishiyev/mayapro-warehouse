using MayaPro.WarehouseApi.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Customers.Infrastructure.Configurations;

public sealed class CustomerDebtAdjustmentConfiguration : IEntityTypeConfiguration<CustomerDebtAdjustment>
{
    public void Configure(EntityTypeBuilder<CustomerDebtAdjustment> builder)
    {
        builder.ToTable("CustomerDebtAdjustments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Amount).IsRequired();
        builder.Property(a => a.Note).HasMaxLength(500);
        builder.Property(a => a.Date).IsRequired();

        builder.HasIndex(a => a.CustomerId);
    }
}
