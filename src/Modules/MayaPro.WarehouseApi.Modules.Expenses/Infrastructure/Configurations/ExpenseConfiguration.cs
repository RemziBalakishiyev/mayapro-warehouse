using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Expenses.Infrastructure.Configurations;

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);

        // Free-form snapshot of the chosen ExpenseType's name at creation time (never an FK — deleting or
        // renaming a type must not touch existing expenses).
        builder.Property(e => e.Category)
            .IsRequired()
            .HasMaxLength(100);

        // Persisted by name ("general"/"product") for a readable, reorder-safe column.
        builder.Property(e => e.Source)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.Amount).IsRequired();
        builder.Property(e => e.Date).IsRequired();
        builder.Property(e => e.ProductName).HasMaxLength(200);
        builder.Property(e => e.Note).HasMaxLength(500);

        builder.HasIndex(e => e.Date);
        builder.HasIndex(e => e.ProductId);
    }
}
