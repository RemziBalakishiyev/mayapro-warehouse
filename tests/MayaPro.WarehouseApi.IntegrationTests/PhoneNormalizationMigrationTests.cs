using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
using MayaPro.WarehouseApi.Modules.Customers.Infrastructure;
using MayaPro.WarehouseApi.Modules.Settings.Infrastructure;
using MayaPro.WarehouseApi.Modules.Suppliers.Infrastructure;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// BE#46, AC-8..AC-11 — the five <c>NormalizePhoneNumbers</c> data migrations, against a real SQL Server.
/// Each test owns a throwaway database (never the shared API test database), brings the schema up to the
/// migration immediately before the one under test, plants legacy rows, and then applies it — the same shape
/// as <c>ExpensesMigrationTests</c>.
/// <para>
/// What is being proved is mostly what does <i>not</i> happen: rows are not lost, unreadable phones are not
/// "fixed" into a guess, and — for <c>identity.Users</c>, where the phone is the login identifier — a
/// collision stops the migration dead instead of surfacing as an opaque unique-index violation with the table
/// half rewritten.
/// </para>
/// </summary>
public sealed class PhoneNormalizationMigrationTests
{
    private static readonly Guid DefaultTenantId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = new("00000000-0000-0000-0000-0000000000a2");

    private const string CustomersBefore = "20260815182138_AddTenantId";
    private const string SuppliersBefore = "20260815182151_AddTenantId";
    private const string AuthBefore = "20260815182100_AddTenantId";
    private const string SettingsBefore = "20260815182249_AddTenantId";
    private const string TenancyBefore = "20260815234449_AddSubscriptionFields";

    // ---------------------------------------------------------------- customers

    /// <summary>TC-31, TC-32, TC-33 — the whole rule on one table: rewrite, leave alone, count, log.</summary>
    [Fact]
    public async Task Customer_Phones_Become_Canonical_While_Unreadable_Values_Survive_Untouched()
    {
        const string database = "MayaProWarehouse_PhoneMigration_Customers";
        await DropDatabaseAsync<CustomersDbContext>(database, CustomersDbContext.Schema, options => new CustomersDbContext(options));

        var messages = new List<string>();
        await using SqlConnection connection = Listening(database, messages);

        await using var db = new CustomersDbContext(Options<CustomersDbContext>(connection, CustomersDbContext.Schema));
        await db.Database.GetService<IMigrator>().MigrateAsync(CustomersBefore);

        // Four convertible spellings (TC-31) …
        Guid spaced = await InsertCustomerAsync(db, "050 123 45 67");
        Guid plus = await InsertCustomerAsync(db, "+994551112233");
        Guid local = await InsertCustomerAsync(db, "0701234567");
        Guid already = await InsertCustomerAsync(db, "994501234567");

        // … and five that must be left exactly as they are (TC-32).
        Guid tooShort = await InsertCustomerAsync(db, "12345");
        Guid letters = await InsertCustomerAsync(db, "xxx");
        Guid tooLong = await InsertCustomerAsync(db, "00994501234567");
        Guid missing = await InsertCustomerAsync(db, null);
        Guid blank = await InsertCustomerAsync(db, "");

        int before = await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [customers].[Customers]");

        await db.Database.MigrateAsync();

        Assert.Equal(before, await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [customers].[Customers]"));

        Assert.Equal("994501234567", await CustomerPhoneAsync(db, spaced));
        Assert.Equal("994551112233", await CustomerPhoneAsync(db, plus));
        Assert.Equal("994701234567", await CustomerPhoneAsync(db, local));
        Assert.Equal("994501234567", await CustomerPhoneAsync(db, already));

        Assert.Equal("12345", await CustomerPhoneAsync(db, tooShort));
        Assert.Equal("xxx", await CustomerPhoneAsync(db, letters));
        Assert.Equal("00994501234567", await CustomerPhoneAsync(db, tooLong));
        Assert.Null(await CustomerPhoneAsync(db, missing));
        Assert.Equal("", await CustomerPhoneAsync(db, blank));

        // TC-33 — three rewritten (the already-canonical one needed no change), three unreadable. NULL and
        // blank are not failures: an optional phone that was never filled in stays empty.
        Assert.Contains(
            "[BE#46] customers.Customers.Phone - normallasdirildi: 3, cevrile bilmedi: 3",
            messages);
    }

    /// <summary>
    /// TC-34, TC-41 — rolling back leaves the canonical data in place (<c>Down</c> only undoes schema, and
    /// this migration has none), and applying it a second time over already-canonical rows changes nothing.
    /// </summary>
    [Fact]
    public async Task Applying_The_Customer_Migration_Twice_Changes_Nothing()
    {
        const string database = "MayaProWarehouse_PhoneMigration_CustomersTwice";
        await DropDatabaseAsync<CustomersDbContext>(database, CustomersDbContext.Schema, options => new CustomersDbContext(options));

        var messages = new List<string>();
        await using SqlConnection connection = Listening(database, messages);

        await using var db = new CustomersDbContext(Options<CustomersDbContext>(connection, CustomersDbContext.Schema));
        await db.Database.GetService<IMigrator>().MigrateAsync(CustomersBefore);

        Guid id = await InsertCustomerAsync(db, "050 123 45 67");
        Guid untouchable = await InsertCustomerAsync(db, "12345");

        await db.Database.MigrateAsync();
        Assert.Equal("994501234567", await CustomerPhoneAsync(db, id));

        // Down() is a no-op by design; it must still run cleanly, and the data must survive it.
        await db.Database.GetService<IMigrator>().MigrateAsync(CustomersBefore);
        Assert.Equal("994501234567", await CustomerPhoneAsync(db, id));

        messages.Clear();
        await db.Database.MigrateAsync();

        Assert.Equal("994501234567", await CustomerPhoneAsync(db, id));
        Assert.Equal("12345", await CustomerPhoneAsync(db, untouchable));
        Assert.Contains(
            "[BE#46] customers.Customers.Phone - normallasdirildi: 0, cevrile bilmedi: 1",
            messages);
    }

    // ---------------------------------------------------------------- suppliers / settings / tenants

    /// <summary>TC-40 — the same rule and the same log line on <c>suppliers.Suppliers</c>.</summary>
    [Fact]
    public async Task Supplier_Phones_Become_Canonical()
    {
        const string database = "MayaProWarehouse_PhoneMigration_Suppliers";
        await DropDatabaseAsync<SuppliersDbContext>(database, SuppliersDbContext.Schema, options => new SuppliersDbContext(options));

        var messages = new List<string>();
        await using SqlConnection connection = Listening(database, messages);

        await using var db = new SuppliersDbContext(Options<SuppliersDbContext>(connection, SuppliersDbContext.Schema));
        await db.Database.GetService<IMigrator>().MigrateAsync(SuppliersBefore);

        Guid converted = await InsertSupplierAsync(db, "+994 50 111 22 33");
        Guid kept = await InsertSupplierAsync(db, "abc");

        await db.Database.MigrateAsync();

        Assert.Equal("994501112233", await PhoneAsync(db, "[suppliers].[Suppliers]", converted));
        Assert.Equal("abc", await PhoneAsync(db, "[suppliers].[Suppliers]", kept));
        Assert.Contains(
            "[BE#46] suppliers.Suppliers.Phone - normallasdirildi: 1, cevrile bilmedi: 1",
            messages);
    }

    /// <summary>TC-40, TC-19 — the store phone printed on invoice headers.</summary>
    [Fact]
    public async Task Store_Settings_Phone_Becomes_Canonical()
    {
        const string database = "MayaProWarehouse_PhoneMigration_Settings";
        await DropDatabaseAsync<SettingsDbContext>(database, SettingsDbContext.Schema, options => new SettingsDbContext(options));

        var messages = new List<string>();
        await using SqlConnection connection = Listening(database, messages);

        await using var db = new SettingsDbContext(Options<SettingsDbContext>(connection, SettingsDbContext.Schema));
        await db.Database.GetService<IMigrator>().MigrateAsync(SettingsBefore);

        Guid id = await InsertStoreSettingsAsync(db, "(012) 555 44 33");

        await db.Database.MigrateAsync();

        Assert.Equal("994125554433", await PhoneAsync(db, "[settings].[StoreSettings]", id));
        Assert.Contains(
            "[BE#46] settings.StoreSettings.Phone - normallasdirildi: 1, cevrile bilmedi: 0",
            messages);
    }

    /// <summary>TC-40 — the shop's contact number. Two shops may share one; there is no unique index.</summary>
    [Fact]
    public async Task Tenant_Phones_Become_Canonical()
    {
        const string database = "MayaProWarehouse_PhoneMigration_Tenants";
        await DropDatabaseAsync<TenancyDbContext>(database, TenancyDbContext.Schema, options => new TenancyDbContext(options));

        var messages = new List<string>();
        await using SqlConnection connection = Listening(database, messages);

        await using var db = new TenancyDbContext(Options<TenancyDbContext>(connection, TenancyDbContext.Schema));
        await db.Database.GetService<IMigrator>().MigrateAsync(TenancyBefore);

        Guid first = await InsertTenantAsync(db, "050-123-45-67");
        Guid second = await InsertTenantAsync(db, "+994 50 123 45 67");

        await db.Database.MigrateAsync();

        Assert.Equal("994501234567", await PhoneAsync(db, "[tenancy].[Tenants]", first));
        Assert.Equal("994501234567", await PhoneAsync(db, "[tenancy].[Tenants]", second));

        // The seeded "İlk Mağaza" row carries a NULL phone: neither converted nor a failure.
        Assert.Contains(
            "[BE#46] tenancy.Tenants.Phone - normallasdirildi: 2, cevrile bilmedi: 0",
            messages);
    }

    // ---------------------------------------------------------------- identity.Users

    /// <summary>TC-40 — the login identifier itself.</summary>
    [Fact]
    public async Task User_Phones_Become_Canonical()
    {
        const string database = "MayaProWarehouse_PhoneMigration_Users";
        await DropDatabaseAsync<AuthDbContext>(database, AuthDbContext.Schema, options => new AuthDbContext(options));

        var messages = new List<string>();
        await using SqlConnection connection = Listening(database, messages);

        await using var db = new AuthDbContext(Options<AuthDbContext>(connection, AuthDbContext.Schema));
        await db.Database.GetService<IMigrator>().MigrateAsync(AuthBefore);

        Guid local = await InsertUserAsync(db, DefaultTenantId, "0501234567", "Sahibkar");
        Guid spaced = await InsertUserAsync(db, DefaultTenantId, "055 111 22 33", "Satici");
        Guid unreadable = await InsertUserAsync(db, DefaultTenantId, "12345", "Sehv");

        int before = await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [identity].[Users]");

        await db.Database.MigrateAsync();

        Assert.Equal(before, await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [identity].[Users]"));
        Assert.Equal("994501234567", await PhoneAsync(db, "[identity].[Users]", local));
        Assert.Equal("994551112233", await PhoneAsync(db, "[identity].[Users]", spaced));
        Assert.Equal("12345", await PhoneAsync(db, "[identity].[Users]", unreadable));
        Assert.Contains(
            "[BE#46] identity.Users.Phone - normallasdirildi: 2, cevrile bilmedi: 1",
            messages);
    }

    /// <summary>
    /// TC-35 — the guard. Two users in one shop whose phones normalize to the same number stop the migration
    /// before a single row is written, and the error names everything an operator needs to fix them by hand.
    /// </summary>
    [Fact]
    public async Task The_Migration_Stops_When_Two_Users_In_One_Shop_Collide()
    {
        const string database = "MayaProWarehouse_PhoneMigration_UsersDuplicate";
        await DropDatabaseAsync<AuthDbContext>(database, AuthDbContext.Schema, options => new AuthDbContext(options));

        await using var db = new AuthDbContext(Options<AuthDbContext>(database, AuthDbContext.Schema));
        await db.Database.GetService<IMigrator>().MigrateAsync(AuthBefore);

        Guid first = await InsertUserAsync(db, DefaultTenantId, "0501234567", "Birinci Sahibkar");
        Guid second = await InsertUserAsync(db, DefaultTenantId, "+994501234567", "Ikinci Sahibkar");
        Guid innocent = await InsertUserAsync(db, DefaultTenantId, "055 111 22 33", "Satici");

        SqlException error = await Assert.ThrowsAsync<SqlException>(() => db.Database.MigrateAsync());

        Assert.Contains(DefaultTenantId.ToString(), error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("994501234567", error.Message, StringComparison.Ordinal);
        Assert.Contains(first.ToString(), error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.ToString(), error.Message, StringComparison.OrdinalIgnoreCase);

        // The transaction rolled back: not one row moved, not even the uncontested one.
        Assert.Equal("0501234567", await PhoneAsync(db, "[identity].[Users]", first));
        Assert.Equal("+994501234567", await PhoneAsync(db, "[identity].[Users]", second));
        Assert.Equal("055 111 22 33", await PhoneAsync(db, "[identity].[Users]", innocent));
    }

    /// <summary>TC-36 — after the duplicate is resolved by hand, the very same migration goes through.</summary>
    [Fact]
    public async Task The_Migration_Succeeds_Once_The_Duplicate_Is_Resolved()
    {
        const string database = "MayaProWarehouse_PhoneMigration_UsersResolved";
        await DropDatabaseAsync<AuthDbContext>(database, AuthDbContext.Schema, options => new AuthDbContext(options));

        await using var db = new AuthDbContext(Options<AuthDbContext>(database, AuthDbContext.Schema));
        await db.Database.GetService<IMigrator>().MigrateAsync(AuthBefore);

        Guid first = await InsertUserAsync(db, DefaultTenantId, "0501234567", "Birinci Sahibkar");
        Guid second = await InsertUserAsync(db, DefaultTenantId, "+994501234567", "Ikinci Sahibkar");

        await Assert.ThrowsAsync<SqlException>(() => db.Database.MigrateAsync());

        // The manual fix an operator would make.
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE [identity].[Users] SET [Phone] = '0559998877' WHERE [Id] = {0}", second);

        await db.Database.MigrateAsync();

        Assert.Equal("994501234567", await PhoneAsync(db, "[identity].[Users]", first));
        Assert.Equal("994559998877", await PhoneAsync(db, "[identity].[Users]", second));
    }

    /// <summary>
    /// TC-37 — uniqueness is tenant-scoped, so the same number in two different shops is not a collision and
    /// must not stop anything. Two shops may genuinely employ the same person.
    /// </summary>
    [Fact]
    public async Task The_Same_Number_In_Two_Different_Shops_Is_Not_A_Duplicate()
    {
        const string database = "MayaProWarehouse_PhoneMigration_UsersTwoShops";
        await DropDatabaseAsync<AuthDbContext>(database, AuthDbContext.Schema, options => new AuthDbContext(options));

        await using var db = new AuthDbContext(Options<AuthDbContext>(database, AuthDbContext.Schema));
        await db.Database.GetService<IMigrator>().MigrateAsync(AuthBefore);

        Guid here = await InsertUserAsync(db, DefaultTenantId, "0501234567", "Birinci Sahibkar");
        Guid there = await InsertUserAsync(db, OtherTenantId, "+994501234567", "Ikinci Sahibkar");

        await db.Database.MigrateAsync();

        Assert.Equal("994501234567", await PhoneAsync(db, "[identity].[Users]", here));
        Assert.Equal("994501234567", await PhoneAsync(db, "[identity].[Users]", there));
    }

    /// <summary>
    /// TC-38, AC-11 — the index is untouched (same name, same two columns, still unique), but now that every
    /// row is canonical it guards canonical values: a second <c>994501234567</c> in the same shop is refused.
    /// </summary>
    [Fact]
    public async Task The_Unique_Index_Is_Unchanged_And_Now_Guards_Canonical_Values()
    {
        const string database = "MayaProWarehouse_PhoneMigration_UsersIndex";
        await DropDatabaseAsync<AuthDbContext>(database, AuthDbContext.Schema, options => new AuthDbContext(options));

        await using var db = new AuthDbContext(Options<AuthDbContext>(database, AuthDbContext.Schema));
        await db.Database.GetService<IMigrator>().MigrateAsync(AuthBefore);

        await InsertUserAsync(db, DefaultTenantId, "050 123 45 67", "Sahibkar");
        await db.Database.MigrateAsync();

        string columns = await ScalarAsync<string>(db,
            """
            SELECT STUFF((SELECT ',' + c.name
                          FROM sys.index_columns ic
                          JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                          WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                          ORDER BY ic.key_ordinal
                          FOR XML PATH('')), 1, 1, '')
            FROM sys.indexes i
            WHERE i.object_id = OBJECT_ID('[identity].[Users]') AND i.name = 'IX_Users_TenantId_Phone'
              AND i.is_unique = 1
            """);
        Assert.Equal("TenantId,Phone", columns);

        SqlException conflict = await Assert.ThrowsAsync<SqlException>(() =>
            InsertUserAsync(db, DefaultTenantId, "994501234567", "Ikinci"));

        Assert.Contains("IX_Users_TenantId_Phone", conflict.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- plumbing

    private static string ConnectionString(string database) =>
        $"Server=localhost;Database={database};Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    private static DbContextOptions<TContext> Options<TContext>(string database, string schema)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(ConnectionString(database), sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", schema)
                .CommandTimeout(120))
            .Options;

    private static DbContextOptions<TContext> Options<TContext>(SqlConnection connection, string schema)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(connection, sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", schema)
                .CommandTimeout(120))
            .Options;

    /// <summary>
    /// A connection that records the migration's <c>RAISERROR(..., 0, 1) WITH NOWAIT</c> lines. Severity-0
    /// messages arrive as <see cref="SqlConnection.InfoMessage"/>, which is the only way to assert on what an
    /// operator actually sees in the migration output.
    /// </summary>
    private static SqlConnection Listening(string database, List<string> messages)
    {
        var connection = new SqlConnection(ConnectionString(database));
        connection.InfoMessage += (_, e) =>
        {
            foreach (SqlError error in e.Errors)
                messages.Add(error.Message);
        };

        return connection;
    }

    /// <summary>
    /// Own database per test, dropped first so every run starts from nothing. The context is built by the
    /// caller's lambda rather than by reflection: these contexts take an optional <c>ICurrentTenant</c>, and
    /// <c>Activator.CreateInstance</c> will not bind a constructor with an unsupplied optional parameter.
    /// </summary>
    private static async Task DropDatabaseAsync<TContext>(
        string database, string schema, Func<DbContextOptions<TContext>, TContext> create)
        where TContext : DbContext
    {
        await using TContext db = create(Options<TContext>(database, schema));
        await db.Database.EnsureDeletedAsync();
    }

    private static async Task<Guid> InsertCustomerAsync(CustomersDbContext db, string? phone)
    {
        Guid id = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [customers].[Customers]
                ([Id],[Name],[Phone],[Note],[Debt],[TenantId],[CreatedAt],[UpdatedAt])
            VALUES ({0}, N'Musteri', {1}, NULL, 0, {2}, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            id, NullableText(phone), DefaultTenantId);

        return id;
    }

    private static async Task<Guid> InsertSupplierAsync(SuppliersDbContext db, string? phone)
    {
        Guid id = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [suppliers].[Suppliers]
                ([Id],[Name],[ContactName],[Phone],[Note],[Debt],[TenantId],[CreatedAt],[UpdatedAt])
            VALUES ({0}, N'Techizatci', NULL, {1}, NULL, 0, {2}, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            id, NullableText(phone), DefaultTenantId);

        return id;
    }

    private static async Task<Guid> InsertStoreSettingsAsync(SettingsDbContext db, string? phone)
    {
        Guid id = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [settings].[StoreSettings]
                ([Id],[StoreName],[OwnerName],[Address],[Phone],[WhatsappTemplate],[Currency],
                 [DefaultMinStock],[Language],[TenantId],[CreatedAt],[UpdatedAt])
            VALUES ({0}, N'Magaza', NULL, NULL, {1}, N'Salam', N'AZN', 10, N'az', {2},
                    SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            id, NullableText(phone), Guid.NewGuid());

        return id;
    }

    private static async Task<Guid> InsertTenantAsync(TenancyDbContext db, string? phone)
    {
        Guid id = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [tenancy].[Tenants]
                ([Id],[Name],[OwnerName],[Phone],[Status],[ExpiresAt],[MonthlyFee],[CreatedAt],[UpdatedAt])
            VALUES ({0}, N'Magaza', N'Sahibkar', {1}, 1, NULL, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            id, NullableText(phone));

        return id;
    }

    private static async Task<Guid> InsertUserAsync(AuthDbContext db, Guid tenantId, string phone, string name)
    {
        Guid id = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [identity].[Users]
                ([Id],[FullName],[Phone],[Email],[PasswordHash],[Role],[IsActive],[MonthlySalary],
                 [TenantId],[CreatedAt],[UpdatedAt])
            VALUES ({0}, {1}, {2}, NULL, N'hash', N'Owner', 1, 0, {3}, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            id, name, phone, tenantId);

        return id;
    }

    /// <summary>
    /// A genuine SQL NULL cannot be passed as a bare <c>null</c> in the parameter array: EF Core looks up a
    /// store type by the CLR type of the value itself and there is none for <c>DBNull</c>. A typed
    /// <see cref="SqlParameter"/> is used verbatim instead — the same trick <c>ExpensesMigrationTests</c> uses.
    /// </summary>
    private static SqlParameter NullableText(string? value) =>
        new("p", System.Data.SqlDbType.NVarChar, 30) { Value = (object?)value ?? DBNull.Value };

    private static Task<string?> CustomerPhoneAsync(CustomersDbContext db, Guid id) =>
        PhoneAsync(db, "[customers].[Customers]", id);

    private static async Task<string?> PhoneAsync(DbContext db, string table, Guid id)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT [Phone] FROM {table} WHERE [Id] = @id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);

        await db.Database.OpenConnectionAsync();
        try
        {
            object? value = await command.ExecuteScalarAsync();
            return value is null or DBNull ? null : (string)value;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<T> ScalarAsync<T>(DbContext db, string sql)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        await db.Database.OpenConnectionAsync();
        try
        {
            return (T)(await command.ExecuteScalarAsync())!;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
