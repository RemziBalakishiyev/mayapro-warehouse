using MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.GetSupplierHistory;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using MayaPro.WarehouseApi.Modules.Suppliers.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Tests;

/// <summary>
/// Unit tests for <see cref="GetSupplierHistoryHandler"/>: an opening-balance adjustment and payments from
/// different moments come back merged and sorted chronologically, oldest first.
/// </summary>
public sealed class GetSupplierHistoryHandlerTests
{
    [Fact]
    public async Task Merges_Adjustment_And_Payments_In_Chronological_Order()
    {
        await using SuppliersDbContext db = NewDb();
        var supplier = Supplier.Create("Təchizatçı", debt: 170);
        db.Suppliers.Add(supplier);

        DateTime baseTime = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        AddAdjustment(db, supplier.Id, 150m, baseTime);
        AddPayment(db, supplier.Id, 50m, baseTime.AddMinutes(10));
        AddPayment(db, supplier.Id, 30m, baseTime.AddMinutes(5));
        await db.SaveChangesAsync();

        var handler = new GetSupplierHistoryHandler(db);

        IReadOnlyList<SupplierHistoryEntryDto> history = await handler.Handle(supplier.Id, default);

        Assert.Equal(3, history.Count);
        Assert.Equal(SupplierHistoryEntryType.InitialDebt, history[0].Type);
        Assert.Equal(150m, history[0].Amount);
        Assert.Equal(30m, history[1].Amount);
        Assert.Equal(50m, history[2].Amount);
        Assert.True(history[0].Date <= history[1].Date);
        Assert.True(history[1].Date <= history[2].Date);
    }

    private static void AddAdjustment(SuppliersDbContext db, Guid supplierId, decimal amount, DateTime date)
    {
        var adjustment = SupplierDebtAdjustment.Create(supplierId, amount, SupplierDebtAdjustment.InitialDebtNote, null);
        db.SupplierDebtAdjustments.Add(adjustment);
        db.Entry(adjustment).Property(a => a.Date).CurrentValue = date;
    }

    private static void AddPayment(SuppliersDbContext db, Guid supplierId, decimal amount, DateTime date)
    {
        var payment = SupplierPayment.Create(supplierId, amount, null, null);
        db.SupplierPayments.Add(payment);
        db.Entry(payment).Property(p => p.Date).CurrentValue = date;
    }

    private static SuppliersDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<SuppliersDbContext>()
            .UseInMemoryDatabase($"suppliers-history-tests-{Guid.NewGuid()}")
            .Options;
        return new SuppliersDbContext(options);
    }
}
