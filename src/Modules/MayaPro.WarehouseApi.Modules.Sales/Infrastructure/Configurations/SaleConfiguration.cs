using MayaPro.WarehouseApi.Modules.Sales.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Sales.Infrastructure.Configurations;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(s => s.SoldByName).HasMaxLength(200);

        // Persist the payment type by name (Cash/Card/Credit) for a readable, reorder-safe column.
        builder.Property(s => s.PaymentType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(s => s.Date);
        builder.HasIndex(s => s.CustomerId);
    }
}
