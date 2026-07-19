using System.Text.Json;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MayaPro.WarehouseApi.Modules.Sales.Infrastructure.Configurations;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    // camelCase so the stored JSON is [{"name":"...","amount":…}] — the same shape as the frontend wire
    // contract and the Products module's expense lines.
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Category).HasMaxLength(100);
        builder.Property(s => s.SoldByName).HasMaxLength(200);

        // Free-form expense lines live inline as a JSON array (nvarchar(max)). A value comparer is required
        // so EF change-tracking treats the collection by value, not by reference. Populated only on manual
        // sales; catalogued sales store "[]".
        var expenseItemsConverter = new ValueConverter<IReadOnlyList<SaleExpenseItem>, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<List<SaleExpenseItem>>(v, JsonOptions) ?? new List<SaleExpenseItem>());

        var expenseItemsComparer = new ValueComparer<IReadOnlyList<SaleExpenseItem>>(
            (a, b) => (a ?? new List<SaleExpenseItem>()).SequenceEqual(b ?? new List<SaleExpenseItem>()),
            v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            v => v.ToList());

        builder.Property(s => s.ExpenseItems)
            .HasConversion(expenseItemsConverter, expenseItemsComparer)
            .HasColumnName("ExpenseItems")
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        // Existing rows predate free-form sales, so default them (and any row that omits it) to a normal sale.
        builder.Property(s => s.IsManual).HasDefaultValue(false);

        // Persist the payment type by name (Cash/Card/Credit) for a readable, reorder-safe column.
        builder.Property(s => s.PaymentType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(s => s.Date);
        builder.HasIndex(s => s.CustomerId);
    }
}
