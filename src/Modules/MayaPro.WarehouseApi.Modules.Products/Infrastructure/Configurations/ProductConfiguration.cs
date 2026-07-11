using MayaPro.WarehouseApi.Modules.Products.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Products.Infrastructure.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Category).HasMaxLength(100);
        builder.Property(p => p.Size).HasMaxLength(50);
        builder.Property(p => p.Color).HasMaxLength(50);
        builder.Property(p => p.Model).HasMaxLength(100);
        builder.Property(p => p.Barcode).HasMaxLength(100);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(10);
        builder.Property(p => p.SupplierId).HasMaxLength(100);
        builder.Property(p => p.Location).HasMaxLength(300);
        builder.Property(p => p.Store).HasMaxLength(100);
        builder.Property(p => p.Warehouse).HasMaxLength(100);
        builder.Property(p => p.Shelf).HasMaxLength(100);
        builder.Property(p => p.Box).HasMaxLength(100);

        // Batch expenses live inline as Expenses_Yol, Expenses_Fehle... (no separate table).
        builder.OwnsOne(p => p.Expenses, expenses =>
        {
            expenses.Property(e => e.Yol).HasColumnName("Expenses_Yol");
            expenses.Property(e => e.Fehle).HasColumnName("Expenses_Fehle");
            expenses.Property(e => e.Yer).HasColumnName("Expenses_Yer");
            expenses.Property(e => e.Paket).HasColumnName("Expenses_Paket");
            expenses.Property(e => e.Diger).HasColumnName("Expenses_Diger");
        });
        builder.Navigation(p => p.Expenses).IsRequired();

        // Barcode is unique, but only where it is actually set — many products may have no barcode.
        builder.HasIndex(p => p.Barcode)
            .IsUnique()
            .HasFilter("[Barcode] <> ''");
    }
}
