using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.CreateSupplier;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using MayaPro.WarehouseApi.Modules.Suppliers.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Tests;

/// <summary>
/// Unit tests for <see cref="CreateSupplierHandler"/>: the debt-free happy path, the opening-debt happy
/// path (writes the debt, the auditable adjustment and a debt-flavoured activity entry) and the
/// negative-debt validation error (nothing is persisted).
/// </summary>
public sealed class CreateSupplierHandlerTests
{
    private static readonly Guid CurrentUserId = Guid.NewGuid();

    [Fact]
    public async Task Debt_Zero_Creates_Supplier_Without_Adjustment_Or_Debt_Wording()
    {
        await using SuppliersDbContext db = NewDb();
        var activityLogger = new FakeActivityLogger();
        CreateSupplierHandler handler = NewHandler(db, activityLogger);

        var result = await handler.Handle(
            new CreateSupplierCommand("Təchizatçı", null, null, null, 0), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.Debt);
        Assert.Equal(0, await db.SupplierDebtAdjustments.CountAsync());

        (string Type, string Message) entry = Assert.Single(activityLogger.Entries);
        Assert.DoesNotContain("ilkin borc", entry.Message);
    }

    [Fact]
    public async Task Debt_Positive_Creates_Supplier_With_Adjustment_And_Debt_Wording()
    {
        await using SuppliersDbContext db = NewDb();
        var activityLogger = new FakeActivityLogger();
        CreateSupplierHandler handler = NewHandler(db, activityLogger);

        var result = await handler.Handle(
            new CreateSupplierCommand("Təchizatçı", null, null, null, 150), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(150m, result.Value.Debt);

        SupplierDebtAdjustment adjustment = Assert.Single(db.SupplierDebtAdjustments);
        Assert.Equal(result.Value.Id, adjustment.SupplierId);
        Assert.Equal(150m, adjustment.Amount);
        Assert.Equal(SupplierDebtAdjustment.InitialDebtNote, adjustment.Note);
        Assert.Equal(CurrentUserId, adjustment.CreatedByUserId);

        (string Type, string Message) entry = Assert.Single(activityLogger.Entries);
        Assert.Contains($"ilkin borc {150m:0.00} AZN", entry.Message);
    }

    [Fact]
    public async Task Negative_Debt_Fails_Validation_And_Persists_Nothing()
    {
        await using SuppliersDbContext db = NewDb();
        var activityLogger = new FakeActivityLogger();
        CreateSupplierHandler handler = NewHandler(db, activityLogger);

        var result = await handler.Handle(
            new CreateSupplierCommand("Təchizatçı", null, null, null, -5), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Borc mənfi ola bilməz", result.Error.Message);
        Assert.Equal(0, await db.Suppliers.CountAsync());
        Assert.Equal(0, await db.SupplierDebtAdjustments.CountAsync());
        Assert.Empty(activityLogger.Entries);
    }

    private static CreateSupplierHandler NewHandler(SuppliersDbContext db, FakeActivityLogger activityLogger) =>
        new(db, new FakeUnitOfWork(db), new CreateSupplierValidator(), activityLogger, new FakeCurrentUser(CurrentUserId));

    private static SuppliersDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<SuppliersDbContext>()
            .UseInMemoryDatabase($"suppliers-tests-{Guid.NewGuid()}")
            .Options;
        return new SuppliersDbContext(options);
    }
}
