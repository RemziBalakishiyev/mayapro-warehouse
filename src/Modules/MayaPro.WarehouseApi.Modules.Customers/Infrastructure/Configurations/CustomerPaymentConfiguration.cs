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

        // BE#27: ICustomersModule.GetPaymentsAsync filters by this column for the debts-kpi endpoint's
        // periodCollected figure — same reasoning as Expenses' index on its own Date column.
        builder.HasIndex(p => p.Date);
    }
}
