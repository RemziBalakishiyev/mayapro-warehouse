using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.GetExpenseTypes;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>
/// Unit tests for <see cref="GetExpenseTypesHandler"/> and <see cref="ExpenseTypeSeeder"/>: the seven
/// default expense types are seeded exactly once, in a stable (name) order. TC-3, AC-3.
/// </summary>
public sealed class GetExpenseTypesHandlerTests
{
    private static readonly string[] ExpectedSeedNames =
    [
        "Yol pulu",
        "Fəhlə pulu",
        "Yer/Anbar xərci",
        "Paket/Qutu",
        "Gömrük",
        "Mağaza xərci",
        "Digər"
    ];

    [Fact]
    public async Task Returns_Every_Type_Ordered_By_Name()
    {
        await using ExpensesDbContext db = NewDb();
        db.ExpenseTypes.Add(ExpenseType.Create("Zibil"));
        db.ExpenseTypes.Add(ExpenseType.Create("Anbar"));
        await db.SaveChangesAsync();

        var handler = new GetExpenseTypesHandler(db);
        var result = await handler.Handle(default);

        Assert.Equal(2, result.Count);
        Assert.Equal("Anbar", result[0].Name);
        Assert.Equal("Zibil", result[1].Name);
    }

    [Fact]
    public async Task Seeder_Inserts_Exactly_The_Seven_Default_Types()
    {
        // BE#35: the seeder writes to the default shop, so read it back through that shop's context.
        await using ExpensesDbContext db = TestDb.New(TenantDefaults.DefaultTenantId);
        var seeder = new ExpenseTypeSeeder(db);

        await seeder.SeedAsync();

        var handler = new GetExpenseTypesHandler(db);
        var result = await handler.Handle(default);

        Assert.Equal(ExpectedSeedNames.Length, result.Count);
        foreach (string name in ExpectedSeedNames)
            Assert.Contains(result, t => t.Name == name);
    }

    [Fact]
    public async Task Seeder_Is_A_NoOp_When_Types_Already_Exist()
    {
        await using ExpensesDbContext db = NewDb();
        db.ExpenseTypes.Add(ExpenseType.Create("Xüsusi növ"));
        await db.SaveChangesAsync();

        var seeder = new ExpenseTypeSeeder(db);
        await seeder.SeedAsync();

        Assert.Equal(1, await db.ExpenseTypes.CountAsync());
    }

    private static ExpensesDbContext NewDb() => TestDb.New();
}
