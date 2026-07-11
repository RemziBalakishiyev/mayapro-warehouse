using MayaPro.WarehouseApi.Modules.Activity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Activity.Infrastructure.Configurations;

public sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("ActivityLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Type).IsRequired().HasMaxLength(50);
        builder.Property(a => a.Message).IsRequired().HasMaxLength(1000);
        builder.Property(a => a.UserName).HasMaxLength(200);

        // The feed is always read newest-first.
        builder.HasIndex(a => a.CreatedAt).IsDescending();
    }
}
