using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Infrastructure.Configurations;

public sealed class SupplierDebtAdjustmentConfiguration : IEntityTypeConfiguration<SupplierDebtAdjustment>
{
    public void Configure(EntityTypeBuilder<SupplierDebtAdjustment> builder)
    {
        builder.ToTable("SupplierDebtAdjustments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Amount).IsRequired();
        builder.Property(a => a.Note).HasMaxLength(500);
        builder.Property(a => a.Date).IsRequired();

        builder.HasIndex(a => a.SupplierId);
    }
}
