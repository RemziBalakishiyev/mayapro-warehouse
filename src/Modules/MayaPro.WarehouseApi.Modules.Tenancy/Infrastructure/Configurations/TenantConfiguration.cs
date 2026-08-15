using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.OwnerName).HasMaxLength(200);
        builder.Property(t => t.Phone).HasMaxLength(30);

        // Stored as int (0/1/2) — the values are part of the migration's seed statement.
        builder.Property(t => t.Status).IsRequired().HasConversion<int>();

        builder.HasIndex(t => t.Status);
    }
}
