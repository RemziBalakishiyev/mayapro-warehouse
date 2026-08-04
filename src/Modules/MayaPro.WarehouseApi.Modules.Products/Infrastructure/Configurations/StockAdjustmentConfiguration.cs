using MayaPro.WarehouseApi.Modules.Products.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Products.Infrastructure.Configurations;

public sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("StockAdjustments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Delta).IsRequired();
        builder.Property(a => a.Date).IsRequired();

        builder.HasIndex(a => a.ProductId);
        builder.HasIndex(a => a.Date);
    }
}
