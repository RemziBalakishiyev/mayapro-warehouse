using System.Data.Common;
using System.Text.Json;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// Verifies data-preserving product migrations against a real SQL Server (throwaway DB, never the shared
/// API test database): attributes JSON, and free-form expense lines from the old bucket columns.
/// </summary>
public sealed class ProductsMigrationTests
{
    // Separate database from the shared API test DB, so migrating it from scratch is isolated.
    private const string ConnectionString =
        "Server=localhost;Database=MayaProWarehouse_MigrationTest;Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    // The migration applied just before CategoriesAndProductAttributes.
    private const string BeforeAttributesMigration = "20260711183344_RenameExpenseColumns";

    // The migration applied just before ProductExpensesAsJsonLines.
    private const string BeforeExpensesJsonMigration = "20260712101733_CategoriesAndProductAttributes";

    [Fact]
    public async Task Migration_Copies_Legacy_Size_Color_Model_Into_Attributes_Json_And_Seeds_Categories()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseSqlServer(ConnectionString, sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", ProductsDbContext.Schema)
                .CommandTimeout(120))
            .Options;

        await using var db = new ProductsDbContext(options);
        await db.Database.EnsureDeletedAsync();

        // Bring the schema up to just before the attributes migration (Size/Color/Model still exist).
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeAttributesMigration);

        Guid fullId = Guid.NewGuid();     // all three set
        Guid partialId = Guid.NewGuid();  // only Size set
        Guid emptyId = Guid.NewGuid();    // none set

        await InsertLegacyRowAsync(db, fullId, "Tam mal", "Şalvar", "30-38", "Tünd göy", "MNG-armani", "MIG-FULL");
        await InsertLegacyRowAsync(db, partialId, "Yarım mal", "Bluz", "S-XL", "", "", "MIG-PARTIAL");
        await InsertLegacyRowAsync(db, emptyId, "Boş mal", "Aksesuar", "", "", "", "MIG-EMPTY");

        // Apply the migration under test.
        await db.Database.MigrateAsync();

        // Full row → three attributes in fixed order.
        List<(string Name, string Value)> full = ParseAttributes(await ReadAttributesAsync(db, fullId));
        Assert.Equal(3, full.Count);
        Assert.Equal(("Ölçü", "30-38"), full[0]);
        Assert.Equal(("Rəng", "Tünd göy"), full[1]);
        Assert.Equal(("Model", "MNG-armani"), full[2]);

        // Partial row → only the filled value is carried over.
        List<(string Name, string Value)> partial = ParseAttributes(await ReadAttributesAsync(db, partialId));
        Assert.Equal(new List<(string, string)> { ("Ölçü", "S-XL") }, partial);

        // Empty row → empty array (no attributes).
        Assert.Empty(ParseAttributes(await ReadAttributesAsync(db, emptyId)));

        // Categories were seeded from the distinct category names on the migrated rows.
        List<string> categories = await ReadCategoryNamesAsync(db);
        Assert.Contains("Şalvar", categories);
        Assert.Contains("Bluz", categories);
        Assert.Contains("Aksesuar", categories);
    }

    [Fact]
    public async Task Migration_Copies_NonZero_Expense_Buckets_Into_Named_Json_Lines()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseSqlServer(ConnectionString, sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", ProductsDbContext.Schema)
                .CommandTimeout(120))
            .Options;

        await using var db = new ProductsDbContext(options);
        await db.Database.EnsureDeletedAsync();

        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeExpensesJsonMigration);

        Guid withExpensesId = Guid.NewGuid();
        Guid emptyId = Guid.NewGuid();

        // Legacy bucket columns still present at this point (Attributes JSON already exists).
        await InsertBucketExpenseRowAsync(db, withExpensesId, "Xərcli mal", transport: 11, labor: 22, storage: 0, packaging: 44, other: 55);
        await InsertBucketExpenseRowAsync(db, emptyId, "Boş xərcli mal", transport: 0, labor: 0, storage: 0, packaging: 0, other: 0);

        await db.Database.MigrateAsync();

        List<(string Name, decimal Amount)> lines = ParseExpenses(await ReadExpensesAsync(db, withExpensesId));
        Assert.Equal(4, lines.Count);
        Assert.Equal(("Yol pulu", 11m), lines[0]);
        Assert.Equal(("Fəhlə pulu", 22m), lines[1]);
        Assert.Equal(("Paket/Qutu", 44m), lines[2]);
        Assert.Equal(("Digər", 55m), lines[3]);

        Assert.Empty(ParseExpenses(await ReadExpensesAsync(db, emptyId)));
    }

    private static Task InsertLegacyRowAsync(
        ProductsDbContext db, Guid id, string name, string category,
        string size, string color, string model, string barcode) =>
        db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [products].[Products]
                ([Id],[Name],[Category],[Size],[Color],[Model],[Barcode],[Image],[Note],
                 [PurchasePrice],[SalePrice],[Quantity],[InitialQuantity],[MinStock],[Currency],
                 [SupplierId],[Location],[Store],[Warehouse],[Shelf],[Box],
                 [Expenses_Transport],[Expenses_Labor],[Expenses_Storage],[Expenses_Packaging],[Expenses_Other],
                 [RealCostPerUnit],[CreatedAt],[UpdatedAt])
            VALUES
                ({0},{1},{2},{3},{4},{5},{6},N'',N'',
                 5,10,10,10,1,N'AZN',
                 N'sup_1',N'',N'',N'',N'',N'',
                 0,0,0,0,0,
                 5,SYSUTCDATETIME(),SYSUTCDATETIME());
            """,
            id, name, category, size, color, model, barcode);

    private static Task InsertBucketExpenseRowAsync(
        ProductsDbContext db, Guid id, string name,
        decimal transport, decimal labor, decimal storage, decimal packaging, decimal other) =>
        db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [products].[Products]
                ([Id],[Name],[Category],[Attributes],[Barcode],[Image],[Note],
                 [PurchasePrice],[SalePrice],[Quantity],[InitialQuantity],[MinStock],[Currency],
                 [SupplierId],[Location],[Store],[Warehouse],[Shelf],[Box],
                 [Expenses_Transport],[Expenses_Labor],[Expenses_Storage],[Expenses_Packaging],[Expenses_Other],
                 [RealCostPerUnit],[CreatedAt],[UpdatedAt])
            VALUES
                ({0},{1},N'Test',N'[]',{2},N'',N'',
                 5,10,10,10,1,N'AZN',
                 N'sup_1',N'',N'',N'',N'',N'',
                 {3},{4},{5},{6},{7},
                 5,SYSUTCDATETIME(),SYSUTCDATETIME());
            """,
            id, name, $"MIG-EXP-{id:N}"[..20], transport, labor, storage, packaging, other);

    private static async Task<string> ReadAttributesAsync(ProductsDbContext db, Guid id) =>
        await ReadStringColumnAsync(db, "Attributes", id);

    private static async Task<string> ReadExpensesAsync(ProductsDbContext db, Guid id) =>
        await ReadStringColumnAsync(db, "Expenses", id);

    private static async Task<string> ReadStringColumnAsync(ProductsDbContext db, string column, Guid id)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT [{column}] FROM [products].[Products] WHERE [Id] = @id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);

        await db.Database.OpenConnectionAsync();
        try
        {
            return (string)(await command.ExecuteScalarAsync())!;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<List<string>> ReadCategoryNamesAsync(ProductsDbContext db)
    {
        var names = new List<string>();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT [Name] FROM [products].[Categories]";

        await db.Database.OpenConnectionAsync();
        try
        {
            await using DbDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                names.Add(reader.GetString(0));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        return names;
    }

    private static List<(string Name, string Value)> ParseAttributes(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray()
            .Select(e => (e.GetProperty("name").GetString()!, e.GetProperty("value").GetString()!))
            .ToList();
    }

    private static List<(string Name, decimal Amount)> ParseExpenses(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray()
            .Select(e => (e.GetProperty("name").GetString()!, e.GetProperty("amount").GetDecimal()))
            .ToList();
    }
}
