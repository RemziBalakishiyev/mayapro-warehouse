using MayaPro.WarehouseApi.Modules.Sales.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// Verifies the data-preserving sales migrations against a real SQL Server (throwaway DB, never the shared
/// API test database): AddSalePurchasePricePerUnit recovers each free-form sale's purchase price from the
/// cost it already stored, leaves catalogued sales alone, and survives the awkward rows (unknown cost,
/// zero quantity, empty or corrupt expense JSON) without failing the deployment; AddSalePaidAmount (BE#15)
/// backfills what every pre-existing sale had actually been paid.
/// </summary>
public sealed class SalesMigrationTests
{
    // Separate database from the shared API test DB, so migrating it from scratch is isolated.
    private const string ConnectionString =
        "Server=localhost;Database=MayaProWarehouse_SalesMigrationTest;Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    // The migration applied just before AddSalePurchasePricePerUnit.
    private const string BeforePurchasePriceMigration = "20260725160000_AddSaleInvoiceToken";

    // The migration applied just before AddSalePaidAmount (BE#15).
    private const string BeforePaidAmountMigration = "20260726142954_AddSalePurchasePricePerUnit";

    [Fact]
    public async Task Migration_Backfills_Purchase_Price_Only_For_Free_Form_Sales()
    {
        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer(ConnectionString, sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", SalesDbContext.Schema)
                .CommandTimeout(120))
            .Options;

        await using var db = new SalesDbContext(options);
        await db.Database.EnsureDeletedAsync();

        // Bring the schema up to just before the migration under test (no PurchasePricePerUnit column yet).
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforePurchasePriceMigration);

        Guid withExpensesId = Guid.NewGuid();   // TC-4: cost carries a per-unit expense share
        Guid noExpensesId = Guid.NewGuid();     // TC-5: no expense lines → cost is the purchase price
        Guid cataloguedId = Guid.NewGuid();     // TC-6: normal sale → never invented
        Guid unknownCostId = Guid.NewGuid();    // TC-7: unknown cost stays unknown
        Guid zeroQuantityId = Guid.NewGuid();   // no division by zero
        Guid corruptJsonId = Guid.NewGuid();    // a broken JSON blob must not abort the deployment
        Guid roundingId = Guid.NewGuid();       // share that does not divide evenly

        await InsertSaleAsync(db, withExpensesId, isManual: true, costPerUnit: 110m, quantity: 2,
            expenseItems: """[{"name":"Yol","amount":10},{"name":"Fəhlə","amount":10}]""");
        await InsertSaleAsync(db, noExpensesId, isManual: true, costPerUnit: 75m, quantity: 4,
            expenseItems: "[]");
        await InsertSaleAsync(db, cataloguedId, isManual: false, costPerUnit: 95m, quantity: 1,
            expenseItems: "[]", productId: Guid.NewGuid());
        await InsertSaleAsync(db, unknownCostId, isManual: true, costPerUnit: null, quantity: 1,
            expenseItems: """[{"name":"Yol","amount":7}]""");
        await InsertSaleAsync(db, zeroQuantityId, isManual: true, costPerUnit: 60m, quantity: 0,
            expenseItems: """[{"name":"Yol","amount":10}]""");
        await InsertSaleAsync(db, corruptJsonId, isManual: true, costPerUnit: 42m, quantity: 1,
            expenseItems: "not json at all");
        await InsertSaleAsync(db, roundingId, isManual: true, costPerUnit: 100m, quantity: 3,
            expenseItems: """[{"name":"Yol","amount":10}]""");

        // Apply the migration under test.
        await db.Database.MigrateAsync();

        // TC-4: 110 − (20 / 2) = 100 — the expense share is peeled back off the cost.
        Assert.Equal(100m, await ReadPurchasePriceAsync(db, withExpensesId));

        // TC-5: nothing to peel back, so the cost *is* the purchase price.
        Assert.Equal(75m, await ReadPurchasePriceAsync(db, noExpensesId));

        // TC-6: a catalogued sale predates any purchase-price snapshot — nothing is invented for it.
        Assert.Null(await ReadPurchasePriceAsync(db, cataloguedId));

        // TC-7: unknown cost → unknown purchase price (never 0, never "expenses reversed out of nothing").
        Assert.Null(await ReadPurchasePriceAsync(db, unknownCostId));

        // Zero quantity: no division, the cost is carried over as-is.
        Assert.Equal(60m, await ReadPurchasePriceAsync(db, zeroQuantityId));

        // Corrupt JSON: treated as "no expense lines" rather than blowing up the whole migration.
        Assert.Equal(42m, await ReadPurchasePriceAsync(db, corruptJsonId));

        // Uneven share: 100 − (10 / 3) = 96.666… → stored rounded to the money scale.
        Assert.Equal(96.67m, await ReadPurchasePriceAsync(db, roundingId));
    }

    [Fact]
    public async Task Migration_Backfills_PaidAmount_From_The_Payment_Type()
    {
        // BE#15 / AC1: every row that existed before the column did was either paid in full (Nağd/Kart) or
        // wholly owed (Nisyə) — the backfill must say exactly that, and must attribute the money to the
        // method it actually arrived by.
        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer(ConnectionString, sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", SalesDbContext.Schema)
                .CommandTimeout(120))
            .Options;

        await using var db = new SalesDbContext(options);
        await db.Database.EnsureDeletedAsync();

        // Bring the schema up to just before the migration under test (no PaidAmount/PaidVia columns yet).
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforePaidAmountMigration);

        Guid cashId = Guid.NewGuid();
        Guid cardId = Guid.NewGuid();
        Guid creditId = Guid.NewGuid();

        await InsertSaleAsync(db, cashId, isManual: true, costPerUnit: 50m, quantity: 1,
            expenseItems: "[]", paymentType: "Cash");
        await InsertSaleAsync(db, cardId, isManual: true, costPerUnit: 50m, quantity: 1,
            expenseItems: "[]", paymentType: "Card");
        await InsertSaleAsync(db, creditId, isManual: true, costPerUnit: 50m, quantity: 1,
            expenseItems: "[]", paymentType: "Credit");

        await db.Database.MigrateAsync();

        // Cash/Card: paid in full at sale time, booked under their own method.
        Assert.Equal((200m, "Cash"), await ReadPaymentAsync(db, cashId));
        Assert.Equal((200m, "Card"), await ReadPaymentAsync(db, cardId));

        // Credit: nothing was received, so nothing may be attributed to the drawer (PaidVia is inert at 0).
        Assert.Equal((0m, "Cash"), await ReadPaymentAsync(db, creditId));
    }

    /// <summary>
    /// Inserts a pre-migration sale row. Written against a raw command (not <c>ExecuteSqlRaw</c>) so the
    /// nullable columns under test — ProductId and CostPerUnit — can be passed as real SQL NULLs.
    /// </summary>
    private static async Task InsertSaleAsync(
        SalesDbContext db, Guid id, bool isManual, decimal? costPerUnit, int quantity, string expenseItems,
        Guid? productId = null, string paymentType = "Cash")
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            """
            INSERT INTO [sales].[Sales]
                ([Id],[ProductId],[IsManual],[ProductName],[Category],[Quantity],[UnitPrice],
                 [Subtotal],[TotalAmount],[CostPerUnit],[Profit],[PaymentType],[CustomerId],
                 [SoldByUserId],[SoldByName],[Date],[ExpenseItems],[CreatedAt],[UpdatedAt])
            VALUES
                (@id,@productId,@isManual,N'Migrasiya malı',NULL,@quantity,200,
                 200,200,@costPerUnit,NULL,@paymentType,NULL,
                 NULL,N'Satıcı',SYSUTCDATETIME(),@expenseItems,SYSUTCDATETIME(),SYSUTCDATETIME());
            """;

        AddParameter(command, "@id", id);
        AddParameter(command, "@productId", productId);
        AddParameter(command, "@isManual", isManual);
        AddParameter(command, "@quantity", quantity);
        AddParameter(command, "@costPerUnit", costPerUnit);
        AddParameter(command, "@expenseItems", expenseItems);
        AddParameter(command, "@paymentType", paymentType);

        await db.Database.OpenConnectionAsync();
        try
        {
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static async Task<(decimal PaidAmount, string PaidVia)> ReadPaymentAsync(SalesDbContext db, Guid id)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT [PaidAmount],[PaidVia] FROM [sales].[Sales] WHERE [Id] = @id";
        AddParameter(command, "@id", id);

        await db.Database.OpenConnectionAsync();
        try
        {
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return (reader.GetDecimal(0), reader.GetString(1));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<decimal?> ReadPurchasePriceAsync(SalesDbContext db, Guid id)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT [PurchasePricePerUnit] FROM [sales].[Sales] WHERE [Id] = @id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);

        await db.Database.OpenConnectionAsync();
        try
        {
            object? value = await command.ExecuteScalarAsync();
            return value is null or DBNull ? null : (decimal)value;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
