using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.DeleteSupplier;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using MayaPro.WarehouseApi.Modules.Suppliers.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Tests;

/// <summary>
/// Unit tests for <see cref="DeleteSupplierHandler"/>. There is no FK cascade behind the supplier's
/// history, so the handler has to clear the opening-balance adjustments itself — a supplier whose opening
/// debt was paid off is deletable and must not leave orphan adjustment rows behind. A supplier we still
/// owe is refused (409) and keeps everything.
/// </summary>
public sealed class DeleteSupplierHandlerTests
{
    [Fact]
    public async Task Deleting_A_Settled_Supplier_Also_Removes_Their_Opening_Balance_History()
    {
        await using SuppliersDbContext db = NewDb();

        // Created with an opening debt that was later paid off in full, so deletion is allowed.
        Supplier supplier = Supplier.Create("Təchizatçı", debt: 150);
        db.Suppliers.Add(supplier);
        db.SupplierDebtAdjustments.Add(
            SupplierDebtAdjustment.Create(supplier.Id, 150m, SupplierDebtAdjustment.InitialDebtNote, null));
        db.SupplierPayments.Add(SupplierPayment.Create(supplier.Id, 150m, null, null));
        Assert.True(supplier.DecreaseDebt(150m).IsSuccess);
        await db.SaveChangesAsync();

        var result = await NewHandler(db).Handle(supplier.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, await db.Suppliers.CountAsync());
        Assert.Equal(0, await db.SupplierDebtAdjustments.CountAsync());
        Assert.Equal(0, await db.SupplierPayments.CountAsync());
    }

    [Fact]
    public async Task Supplier_We_Still_Owe_Is_Refused_And_Keeps_Their_History()
    {
        await using SuppliersDbContext db = NewDb();

        Supplier supplier = Supplier.Create("Borclu təchizatçı", debt: 150);
        db.Suppliers.Add(supplier);
        db.SupplierDebtAdjustments.Add(
            SupplierDebtAdjustment.Create(supplier.Id, 150m, SupplierDebtAdjustment.InitialDebtNote, null));
        await db.SaveChangesAsync();

        var result = await NewHandler(db).Handle(supplier.Id, default);

        Assert.True(result.IsFailure);
        Assert.Equal(SupplierErrors.HasDebtConflict, result.Error);
        Assert.Equal(1, await db.Suppliers.CountAsync());
        Assert.Equal(1, await db.SupplierDebtAdjustments.CountAsync());
    }

    [Fact]
    public async Task Unknown_Supplier_Returns_NotFound()
    {
        await using SuppliersDbContext db = NewDb();

        var result = await NewHandler(db).Handle(Guid.NewGuid(), default);

        Assert.True(result.IsFailure);
        Assert.Equal(SupplierErrors.NotFound, result.Error);
    }

    private static DeleteSupplierHandler NewHandler(SuppliersDbContext db) =>
        new(db, new FakeUnitOfWork(db), new FakeActivityLogger(), new FakeCurrentUser(Guid.NewGuid()));

    private static SuppliersDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<SuppliersDbContext>()
            .UseInMemoryDatabase($"suppliers-delete-tests-{Guid.NewGuid()}")
            .Options;
        return new SuppliersDbContext(options);
    }
}
