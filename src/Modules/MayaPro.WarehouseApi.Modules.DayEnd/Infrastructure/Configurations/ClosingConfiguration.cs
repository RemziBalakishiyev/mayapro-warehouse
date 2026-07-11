using MayaPro.WarehouseApi.Modules.DayEnd.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Infrastructure.Configurations;

public sealed class ClosingConfiguration : IEntityTypeConfiguration<Closing>
{
    public void Configure(EntityTypeBuilder<Closing> builder)
    {
        builder.ToTable("Closings");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Date).IsRequired();
        builder.Property(c => c.Note).HasMaxLength(500);

        // A day can be closed only once — the guard against a concurrent second close.
        builder.HasIndex(c => c.Date).IsUnique();
    }
}
