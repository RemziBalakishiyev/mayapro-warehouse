using System.Text.Json;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MayaPro.WarehouseApi.Modules.Products.Infrastructure.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    // camelCase so the stored JSON is [{"name":"...","value":"..."}] / [{"name":"...","amount":…}] —
    // the exact shape the data-migration SQL writes and the frontend wire contract uses.
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Category).HasMaxLength(100);
        builder.Property(p => p.Barcode).HasMaxLength(100);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(10);
        builder.Property(p => p.SupplierId).HasMaxLength(100);
        builder.Property(p => p.Location).HasMaxLength(300);
        builder.Property(p => p.Store).HasMaxLength(100);
        builder.Property(p => p.Warehouse).HasMaxLength(100);
        builder.Property(p => p.Shelf).HasMaxLength(100);
        builder.Property(p => p.Box).HasMaxLength(100);

        // Dynamic attributes live inline as a JSON array (nvarchar(max)). A value comparer is required so EF
        // change-tracking treats the collection by value, not by reference.
        var attributesConverter = new ValueConverter<IReadOnlyList<ProductAttribute>, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<List<ProductAttribute>>(v, JsonOptions) ?? new List<ProductAttribute>());

        var attributesComparer = new ValueComparer<IReadOnlyList<ProductAttribute>>(
            (a, b) => (a ?? new List<ProductAttribute>()).SequenceEqual(b ?? new List<ProductAttribute>()),
            v => v.Aggregate(0, (hash, attr) => HashCode.Combine(hash, attr.GetHashCode())),
            v => v.ToList());

        builder.Property(p => p.Attributes)
            .HasConversion(attributesConverter, attributesComparer)
            .HasColumnName("Attributes")
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        // Free-form batch expenses — same JSON array pattern as attributes.
        var expensesConverter = new ValueConverter<IReadOnlyList<ProductExpenseItem>, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<List<ProductExpenseItem>>(v, JsonOptions) ?? new List<ProductExpenseItem>());

        var expensesComparer = new ValueComparer<IReadOnlyList<ProductExpenseItem>>(
            (a, b) => (a ?? new List<ProductExpenseItem>()).SequenceEqual(b ?? new List<ProductExpenseItem>()),
            v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            v => v.ToList());

        builder.Property(p => p.Expenses)
            .HasConversion(expensesConverter, expensesComparer)
            .HasColumnName("Expenses")
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        // Barcode is unique, but only where it is actually set — many products may have no barcode.
        builder.HasIndex(p => p.Barcode)
            .IsUnique()
            .HasFilter("[Barcode] <> ''");
    }
}
