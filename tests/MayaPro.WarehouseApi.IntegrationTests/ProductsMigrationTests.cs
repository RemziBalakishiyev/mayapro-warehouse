using System.Data.Common;
using System.Text.Json;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// Verifies the data-preserving half of the <c>CategoriesAndProductAttributes</c> migration against a real
/// SQL Server: it runs the schema up to the migration BEFORE it, inserts legacy-format rows (with the old
/// Size/Color/Model columns still present), then applies the migration and asserts that the values were
/// copied into the JSON <c>Attributes</c> column — only the non-empty ones, in Ölçü/Rəng/Model order — and
/// that the categories were seeded from the existing category names.
/// <para>Uses its own throwaway database so it never touches the shared API test database.</para>
/// </summary>
public sealed class ProductsMigrationTests
{
    // Separate database from the shared API test DB, so migrating it from scratch is isolated.
    private const string ConnectionString =
        "Server=localhost;Database=MayaProWarehouse_MigrationTest;Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    // The migration applied just before the one under test.
    private const string PreviousMigration = "20260711183344_RenameExpenseColumns";

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
        await migrator.MigrateAsync(PreviousMigration);

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

    private static async Task<string> ReadAttributesAsync(ProductsDbContext db, Guid id)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT [Attributes] FROM [products].[Products] WHERE [Id] = @id";
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
}
