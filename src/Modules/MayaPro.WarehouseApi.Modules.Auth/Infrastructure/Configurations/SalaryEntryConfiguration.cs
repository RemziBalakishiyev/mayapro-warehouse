using MayaPro.WarehouseApi.Modules.Auth.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure.Configurations;

public sealed class SalaryEntryConfiguration : IEntityTypeConfiguration<SalaryEntry>
{
    public void Configure(EntityTypeBuilder<SalaryEntry> builder)
    {
        builder.ToTable("SalaryEntries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).IsRequired();

        // Persisted by name ("Payment"/"Deduction") for a readable, reorder-safe column.
        builder.Property(e => e.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.Amount).IsRequired();
        builder.Property(e => e.Date).IsRequired();

        // The yyyy-MM accounting month — fixed width, filtered on by the salary summary.
        builder.Property(e => e.Month)
            .IsRequired()
            .HasMaxLength(7);

        builder.Property(e => e.Note).HasMaxLength(500);

        // The cash-side lookup (day-end / dashboard) reads Date; the summary reads (UserId, Month).
        builder.HasIndex(e => e.Date);
        builder.HasIndex(e => new { e.UserId, e.Month });
    }
}
