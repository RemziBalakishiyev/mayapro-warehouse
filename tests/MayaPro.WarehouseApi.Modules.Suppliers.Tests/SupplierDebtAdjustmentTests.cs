using MayaPro.WarehouseApi.Modules.Suppliers.Domain;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Tests;

/// <summary>Domain unit tests for <see cref="SupplierDebtAdjustment"/> — the opening-balance record.</summary>
public sealed class SupplierDebtAdjustmentTests
{
    [Fact]
    public void Create_Sets_All_Fields_And_Stamps_The_Current_UTC_Time()
    {
        Guid supplierId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTime before = DateTime.UtcNow;

        SupplierDebtAdjustment adjustment =
            SupplierDebtAdjustment.Create(supplierId, 150m, SupplierDebtAdjustment.InitialDebtNote, userId);

        DateTime after = DateTime.UtcNow;

        Assert.Equal(supplierId, adjustment.SupplierId);
        Assert.Equal(150m, adjustment.Amount);
        Assert.Equal("İlkin borc (sistemə keçid)", adjustment.Note);
        Assert.Equal(userId, adjustment.CreatedByUserId);
        Assert.InRange(adjustment.Date, before, after);
    }
}
