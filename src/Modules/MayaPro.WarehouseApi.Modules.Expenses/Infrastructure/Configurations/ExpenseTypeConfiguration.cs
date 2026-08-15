using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Expenses.Infrastructure.Configurations;

public sealed class ExpenseTypeConfiguration : IEntityTypeConfiguration<ExpenseType>
{
    public void Configure(EntityTypeBuilder<ExpenseType> builder)
    {
        builder.ToTable("ExpenseTypes");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);

        // One row per expense type name — per shop (BE#35).
        builder.HasIndex(t => new { t.TenantId, t.Name }).IsUnique();
    }
}
