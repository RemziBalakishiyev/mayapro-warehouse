using MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// Verifies the data-preserving <c>ExpenseTypesAndSource</c> migration against a real SQL Server (throwaway
/// DB, never the shared API test database): old enum category names are rewritten to their Azerbaijani
/// expense-type names, and <c>Source</c> is backfilled from whether the row already carries a
/// <c>ProductId</c>. AC-7, TC-8, TC-9.
/// </summary>
public sealed class ExpensesMigrationTests
{
    // Separate database from the shared API test DB, so migrating it from scratch is isolated.
    private const string ConnectionString =
        "Server=localhost;Database=MayaProWarehouse_ExpensesMigrationTest;Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    // The migration applied just before ExpenseTypesAndSource — Category is still the old 20-char enum
    // column and there is no Source column yet.
    private const string BeforeMigration = "20260711183456_RenameCategoryValues";

    [Fact]
    public async Task Migration_Renames_Legacy_Categories_And_Backfills_Source_From_ProductId()
    {
        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseSqlServer(ConnectionString, sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", ExpensesDbContext.Schema)
                .CommandTimeout(120))
            .Options;

        await using var db = new ExpensesDbContext(options);
        await db.Database.EnsureDeletedAsync();

        // Bring the schema up to just before the migration under test (old enum-backed Category, no Source).
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeMigration);

        Guid productLinkedId = Guid.NewGuid();  // ProductId set → must become Source = product
        Guid generalId = Guid.NewGuid();        // ProductId null → must become Source = general

        await InsertLegacyRowAsync(db, productLinkedId, "Karqo", "Transport", 100m, "Bir qeyd", Guid.NewGuid());
        await InsertLegacyRowAsync(db, generalId, "Mağaza icarəsi", "Store", 600m, null, null);

        int countBefore = await CountExpensesAsync(db);

        // Apply the migration under test.
        await db.Database.MigrateAsync();

        int countAfter = await CountExpensesAsync(db);
        Assert.Equal(countBefore, countAfter); // no rows lost or duplicated

        (string Category, string Source, decimal Amount, string? Note) productLinked =
            await ReadRowAsync(db, productLinkedId);
        Assert.Equal("Yol pulu", productLinked.Category);   // Transport → Yol pulu
        Assert.Equal("product", productLinked.Source);      // ProductId was set
        Assert.Equal(100m, productLinked.Amount);            // untouched
        Assert.Equal("Bir qeyd", productLinked.Note);        // untouched

        (string Category, string Source, decimal Amount, string? Note) general =
            await ReadRowAsync(db, generalId);
        Assert.Equal("Mağaza xərci", general.Category);      // Store → Mağaza xərci
        Assert.Equal("general", general.Source);             // ProductId was null
        Assert.Equal(600m, general.Amount);
    }

    [Theory]
    [InlineData("Transport", "Yol pulu")]
    [InlineData("Labor", "Fəhlə pulu")]
    [InlineData("Storage", "Yer/Anbar xərci")]
    [InlineData("Packaging", "Paket/Qutu")]
    [InlineData("Store", "Mağaza xərci")]
    [InlineData("Other", "Digər")]
    public async Task Migration_Maps_Every_Legacy_Category_To_Its_Azerbaijani_Name(string legacy, string expected)
    {
        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseSqlServer(ConnectionString, sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", ExpensesDbContext.Schema)
                .CommandTimeout(120))
            .Options;

        await using var db = new ExpensesDbContext(options);
        await db.Database.EnsureDeletedAsync();

        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeMigration);

        Guid id = Guid.NewGuid();
        await InsertLegacyRowAsync(db, id, "Test xərci", legacy, 1m, null, null);

        await db.Database.MigrateAsync();

        (string Category, string Source, decimal Amount, string? Note) row = await ReadRowAsync(db, id);
        Assert.Equal(expected, row.Category);
    }

    [Fact]
    public async Task Migration_Leaves_Source_Required_And_Without_A_Lingering_Default_Constraint()
    {
        // The backfill fills both branches explicitly instead of adding the column with a SQL default:
        // a default constraint would survive on the table without existing in the EF model (schema drift),
        // and would silently paper over a future NULL.
        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseSqlServer(ConnectionString, sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", ExpensesDbContext.Schema)
                .CommandTimeout(120))
            .Options;

        await using var db = new ExpensesDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        object? nullable = await ScalarAsync(db,
            """
            SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'expenses' AND TABLE_NAME = 'Expenses' AND COLUMN_NAME = 'Source'
            """);
        Assert.Equal("NO", nullable);

        object? defaults = await ScalarAsync(db,
            """
            SELECT COUNT(*) FROM sys.default_constraints dc
            JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID('[expenses].[Expenses]') AND c.name = 'Source'
            """);
        Assert.Equal(0, defaults);
    }

    private static async Task<object?> ScalarAsync(ExpensesDbContext db, string sql)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        await db.Database.OpenConnectionAsync();
        try
        {
            return await command.ExecuteScalarAsync();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static Task InsertLegacyRowAsync(
        ExpensesDbContext db, Guid id, string name, string category, decimal amount, string? note, Guid? productId) =>
        db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [expenses].[Expenses]
                ([Id],[Name],[Category],[Amount],[Date],[ProductId],[ProductName],[Note],
                 [CreatedByUserId],[CreatedAt],[UpdatedAt])
            VALUES
                ({0},{1},{2},{3},SYSUTCDATETIME(),{4},NULL,{5},
                 NULL,SYSUTCDATETIME(),SYSUTCDATETIME());
            """,
            // The two optional columns go through DBNull — a plain null is not a legal params object[] entry.
            id, name, category, amount, (object?)productId ?? DBNull.Value, (object?)note ?? DBNull.Value);

    private static async Task<int> CountExpensesAsync(ExpensesDbContext db)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM [expenses].[Expenses]";

        await db.Database.OpenConnectionAsync();
        try
        {
            return (int)(await command.ExecuteScalarAsync())!;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<(string Category, string Source, decimal Amount, string? Note)> ReadRowAsync(
        ExpensesDbContext db, Guid id)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "SELECT [Category], [Source], [Amount], [Note] FROM [expenses].[Expenses] WHERE [Id] = @id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);

        await db.Database.OpenConnectionAsync();
        try
        {
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            return (
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.IsDBNull(3) ? null : reader.GetString(3));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
