using MayaPro.WarehouseApi.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Customers.Infrastructure.Configurations;

public sealed class CustomerPaymentConfiguration : IEntityTypeConfiguration<CustomerPayment>
{
    public void Configure(EntityTypeBuilder<CustomerPayment> builder)
    {
        builder.ToTable("CustomerPayments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).IsRequired();
        builder.Property(p => p.Note).HasMaxLength(500);
        builder.Property(p => p.Date).IsRequired();

        builder.HasIndex(p => p.CustomerId);
    }
}
