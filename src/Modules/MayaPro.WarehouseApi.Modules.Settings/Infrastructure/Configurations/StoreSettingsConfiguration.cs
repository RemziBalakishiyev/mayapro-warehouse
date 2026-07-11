using MayaPro.WarehouseApi.Modules.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Settings.Infrastructure.Configurations;

public sealed class StoreSettingsConfiguration : IEntityTypeConfiguration<StoreSettings>
{
    public void Configure(EntityTypeBuilder<StoreSettings> builder)
    {
        builder.ToTable("StoreSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.StoreName).IsRequired().HasMaxLength(200);
        builder.Property(s => s.OwnerName).HasMaxLength(200);
        builder.Property(s => s.WhatsappTemplate).IsRequired().HasMaxLength(1000);
        builder.Property(s => s.Currency).IsRequired().HasMaxLength(10);
        builder.Property(s => s.Language).IsRequired().HasMaxLength(10);
    }
}
