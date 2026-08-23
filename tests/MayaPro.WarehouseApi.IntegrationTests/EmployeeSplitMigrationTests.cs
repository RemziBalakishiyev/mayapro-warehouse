using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
using MayaPro.WarehouseApi.Modules.Auth.Infrastructure.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// BE#57, AC-4 / TC-12..TC-14 — the <c>SplitEmployeeFromUser</c> data migration against a real SQL Server.
/// Each test owns a throwaway database (never the shared API test database), brings the schema up to the
/// migration immediately before the one under test, plants legacy rows and then applies it — the same shape
/// as <c>PhoneNormalizationMigrationTests</c>.
/// <para>
/// What is being proved is almost entirely what does <b>not</b> happen. The salary history is real money that
/// really left the drawer: not one row may be lost, no figure may change, and the login accounts must come
/// through the split untouched.
/// </para>
/// </summary>
public sealed class EmployeeSplitMigrationTests
{
    private static readonly Guid DefaultTenantId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = new("00000000-0000-0000-0000-0000000000a2");

    /// <summary>The migration applied just before the split: Users still carry MonthlySalary, SalaryEntries still carry UserId.</summary>
    private const string Before = "20260822183001_NormalizePhoneNumbers";

    /// <summary>
    /// TC-12 — the headline case. Four accounts (one Owner, three staff) with agreed salaries and a handful
    /// of salary lines. Afterwards there are three payroll records carrying the same names, numbers, salaries
    /// and tenants; every salary line still exists, still holds the same money, and now points at its
    /// employee; and all four login accounts are still there.
    /// </summary>
    [Fact]
    public async Task Payroll_Is_Copied_Out_Of_Users_Without_Losing_A_Single_Salary_Line()
    {
        const string database = "MayaProWarehouse_Be57_Split";
        await using AuthDbContext db = await FreshAsync(database);

        Guid owner = await InsertUserAsync(db, DefaultTenantId, "Rəşad Məmmədov", "994501112233", "Owner");
        Guid manager = await InsertUserAsync(db, DefaultTenantId, "Nigar Əliyeva", "994552223344", "Manager", 800m);
        Guid seller = await InsertUserAsync(db, DefaultTenantId, "Elvin Hüseynov", "994553334455", "Seller", 600m);
        Guid inactive = await InsertUserAsync(
            db, DefaultTenantId, "Günel Quliyeva", "994554445566", "Seller", 500m, isActive: false);

        Guid advance = await InsertEntryAsync(db, DefaultTenantId, seller, "Payment", 100m, "2026-03", owner, "Avans");
        await InsertEntryAsync(db, DefaultTenantId, seller, "Deduction", 30m, "2026-03", owner);
        await InsertEntryAsync(db, DefaultTenantId, manager, "Payment", 800m, "2026-02", owner);
        await InsertEntryAsync(db, DefaultTenantId, inactive, "Payment", 250m, "2026-03", owner);

        int entriesBefore = await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [identity].[SalaryEntries]");
        int usersBefore = await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [identity].[Users]");

        await db.Database.MigrateAsync();

        // Not one salary line lost or duplicated, and no account touched (AC-5).
        Assert.Equal(entriesBefore, await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [identity].[SalaryEntries]"));
        Assert.Equal(usersBefore, await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [identity].[Users]"));

        // Three payroll records: the Owner has no salary data, so he is not on the payroll.
        Assert.Equal(3, await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [identity].[Employees]"));
        Assert.Equal(
            0,
            await ScalarAsync<int>(db,
                "SELECT COUNT(*) FROM [identity].[Employees] WHERE [FullName] = N'Rəşad Məmmədov'"));

        // Field by field, the copy is faithful — including the role rendered as a readable job title.
        EmployeeRow copied = await EmployeeAsync(db, "Nigar Əliyeva");
        Assert.Equal(DefaultTenantId, copied.TenantId);
        Assert.Equal("994552223344", copied.Phone);
        Assert.Equal("Menecer", copied.Position);
        Assert.Equal(800m, copied.MonthlySalary);
        Assert.True(copied.IsActive);

        Assert.Equal("Satıcı", (await EmployeeAsync(db, "Elvin Hüseynov")).Position);

        // An account that was switched off arrives switched off.
        Assert.False((await EmployeeAsync(db, "Günel Quliyeva")).IsActive);

        // Every line is mapped, and the money is byte-for-byte what it was.
        Assert.Equal(
            0,
            await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [identity].[SalaryEntries] WHERE [EmployeeId] IS NULL"));

        EntryRow line = await EntryAsync(db, advance);
        Assert.Equal(100m, line.Amount);
        Assert.Equal("Payment", line.Type);
        Assert.Equal("2026-03", line.Month);
        Assert.Equal("Avans", line.Note);
        Assert.Equal((await EmployeeAsync(db, "Elvin Hüseynov")).Id, line.EmployeeId);

        // "Who paid" is still the login account that recorded it — the two ids never merged.
        Assert.Equal(owner, line.CreatedByUserId);

        // The old columns are gone, but only after all of the above was true.
        Assert.False(await ColumnExistsAsync(db, "SalaryEntries", "UserId"));
        Assert.False(await ColumnExistsAsync(db, "Users", "MonthlySalary"));
        Assert.True(await ColumnExistsAsync(db, "SalaryEntries", "EmployeeId"));
    }

    /// <summary>
    /// TC-12 — an Owner who really was paying himself a salary keeps it. The rule is "non-Owner, plus anyone
    /// carrying salary data", precisely so no existing figure can vanish because of someone's role.
    /// </summary>
    [Fact]
    public async Task An_Owner_With_Salary_Data_Still_Becomes_A_Payroll_Record()
    {
        const string database = "MayaProWarehouse_Be57_PaidOwner";
        await using AuthDbContext db = await FreshAsync(database);

        Guid paidOwner = await InsertUserAsync(db, DefaultTenantId, "Sahibkar Maaşlı", "994501112233", "Owner", 1200m);
        Guid plainOwner = await InsertUserAsync(db, DefaultTenantId, "Sahibkar Maaşsız", "994501112244", "Owner");
        await InsertEntryAsync(db, DefaultTenantId, paidOwner, "Payment", 400m, "2026-03", paidOwner);

        await db.Database.MigrateAsync();

        EmployeeRow owner = await EmployeeAsync(db, "Sahibkar Maaşlı");
        Assert.Equal("Sahibkar", owner.Position);
        Assert.Equal(1200m, owner.MonthlySalary);

        Assert.Equal(
            0,
            await ScalarAsync<int>(db,
                "SELECT COUNT(*) FROM [identity].[Employees] WHERE [FullName] = N'Sahibkar Maaşsız'"));
        Assert.NotEqual(Guid.Empty, plainOwner);

        Assert.Equal(
            0,
            await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [identity].[SalaryEntries] WHERE [EmployeeId] IS NULL"));
    }

    /// <summary>
    /// TC-13 — the orphan case, and the reason the fallback exists. <c>SalaryEntries.UserId</c> never had a
    /// foreign key, so a line can point at an account that has since been deleted. Dropping those lines would
    /// quietly reduce what the shop believes it has spent, so each affected tenant gets one inactive
    /// "Naməlum işçi (köçürülmüş)" record — one per shop, not one per line.
    /// </summary>
    [Fact]
    public async Task Salary_Lines_Whose_Account_Is_Gone_Are_Rescued_Not_Dropped()
    {
        const string database = "MayaProWarehouse_Be57_Orphans";
        await using AuthDbContext db = await FreshAsync(database);

        Guid seller = await InsertUserAsync(db, DefaultTenantId, "Elvin Hüseynov", "994553334455", "Seller", 600m);
        await InsertEntryAsync(db, DefaultTenantId, seller, "Payment", 100m, "2026-03", seller);

        // Three lines pointing at accounts that no longer exist — two in one shop, one in another.
        Guid deleted = Guid.NewGuid();
        Guid orphanA = await InsertEntryAsync(db, DefaultTenantId, deleted, "Payment", 70m, "2026-03", seller);
        Guid orphanB = await InsertEntryAsync(db, DefaultTenantId, Guid.NewGuid(), "Deduction", 20m, "2026-03", seller);
        Guid orphanC = await InsertEntryAsync(db, OtherTenantId, Guid.NewGuid(), "Payment", 45m, "2026-04", seller);

        int before = await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [identity].[SalaryEntries]");

        await db.Database.MigrateAsync();

        Assert.Equal(before, await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [identity].[SalaryEntries]"));
        Assert.Equal(
            0,
            await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [identity].[SalaryEntries] WHERE [EmployeeId] IS NULL"));

        // One rescue record per tenant, not per line, and each is inactive — nobody works there.
        Assert.Equal(
            2,
            await ScalarAsync<int>(db,
                "SELECT COUNT(*) FROM [identity].[Employees] WHERE [FullName] = N'Naməlum işçi (köçürülmüş)'"));
        Assert.Equal(
            0,
            await ScalarAsync<int>(db,
                """
                SELECT COUNT(*) FROM [identity].[Employees]
                WHERE [FullName] = N'Naməlum işçi (köçürülmüş)' AND [IsActive] = 1
                """));

        // The two orphans from one shop share a single rescue record; the third shop's has its own.
        EntryRow a = await EntryAsync(db, orphanA);
        EntryRow b = await EntryAsync(db, orphanB);
        EntryRow c = await EntryAsync(db, orphanC);
        Assert.Equal(a.EmployeeId, b.EmployeeId);
        Assert.NotEqual(a.EmployeeId, c.EmployeeId);

        // And the amounts came through untouched.
        Assert.Equal(70m, a.Amount);
        Assert.Equal(20m, b.Amount);
        Assert.Equal(45m, c.Amount);
        Assert.NotEqual(Guid.Empty, deleted);
    }

    /// <summary>
    /// TC-12 — two shops' payroll never mixes: each employee lands in the tenant its account belonged to,
    /// which is what keeps the query filter's guarantee true after the split.
    /// </summary>
    [Fact]
    public async Task Each_Payroll_Record_Stays_In_Its_Own_Shop()
    {
        const string database = "MayaProWarehouse_Be57_Tenants";
        await using AuthDbContext db = await FreshAsync(database);

        await InsertUserAsync(db, DefaultTenantId, "Birinci Satıcı", "994553334455", "Seller", 600m);
        await InsertUserAsync(db, OtherTenantId, "İkinci Satıcı", "994553334455", "Seller", 700m);

        await db.Database.MigrateAsync();

        Assert.Equal(DefaultTenantId, (await EmployeeAsync(db, "Birinci Satıcı")).TenantId);
        Assert.Equal(OtherTenantId, (await EmployeeAsync(db, "İkinci Satıcı")).TenantId);
    }

    /// <summary>An empty database migrates cleanly: nothing to copy is not an error.</summary>
    [Fact]
    public async Task An_Empty_Database_Migrates_Cleanly()
    {
        const string database = "MayaProWarehouse_Be57_Empty";
        await using AuthDbContext db = await FreshAsync(database);

        await db.Database.MigrateAsync();

        Assert.Equal(0, await ScalarAsync<int>(db, "SELECT COUNT(*) FROM [identity].[Employees]"));
        Assert.False(await ColumnExistsAsync(db, "Users", "MonthlySalary"));
    }

    /// <summary>
    /// AC-4 / TC-14 — the ordering guarantee, asserted on the migration's own operation list.
    /// <para>
    /// This is what makes the guard meaningful. The verification step (a <c>THROW</c> if any
    /// <c>EmployeeId</c> is still NULL) only protects the salary history if it runs <b>before</b> anything is
    /// destroyed: every drop must come after it, so a failure rolls back with <c>SalaryEntries.UserId</c> and
    /// <c>Users.MonthlySalary</c> still in place. Note that with the per-tenant fallback in place the guard is
    /// unreachable by construction — which is the point — so its safety is pinned here rather than by
    /// deliberately corrupting a database.
    /// </para>
    /// </summary>
    [Fact]
    public void Nothing_Is_Dropped_Before_The_Copy_Has_Been_Verified()
    {
        var migration = new SplitEmployeeFromUser();
        IReadOnlyList<MigrationOperation> operations = migration.UpOperations;

        int guard = operations
            .Select((operation, index) => (operation, index))
            .Single(x => x.operation is SqlOperation sql
                      && sql.Sql.Contains("THROW 50057", StringComparison.Ordinal))
            .index;

        int firstDestructive = operations
            .Select((operation, index) => (operation, index))
            .First(x => x.operation is DropColumnOperation or DropIndexOperation or DropTableOperation)
            .index;

        Assert.True(
            guard < firstDestructive,
            "BE#57: yoxlama (THROW) hər hansı sütun/indeks silinməsindən ƏVVƏL olmalıdır");

        // The table has to exist before anything is copied into it, and the new link has to exist before the
        // copy can fill it.
        int createTable = operations.Select((o, i) => (o, i)).Single(x => x.o is CreateTableOperation).i;
        int addColumn = operations.Select((o, i) => (o, i)).Single(x => x.o is AddColumnOperation).i;
        Assert.True(createTable < guard);
        Assert.True(addColumn < guard);

        // The column is added nullable on purpose: "not mapped yet" must be a state the guard can see.
        var added = (AddColumnOperation)operations[addColumn];
        Assert.Equal("EmployeeId", added.Name);
        Assert.True(added.IsNullable);

        // …and is only tightened to NOT NULL after the guard has passed.
        int tighten = operations.Select((o, i) => (o, i)).Single(x => x.o is AlterColumnOperation).i;
        Assert.True(guard < tighten);
    }

    /// <summary>
    /// TC-14 — the failure message an operator would actually read: it says what stopped, that nothing was
    /// deleted, and what to do next.
    /// </summary>
    [Fact]
    public void The_Guard_Explains_Itself_In_Azerbaijani()
    {
        string sql = string.Concat(new SplitEmployeeFromUser().UpOperations
            .OfType<SqlOperation>()
            .Select(o => o.Sql));

        Assert.Contains("[BE#57]", sql, StringComparison.Ordinal);
        Assert.Contains("EmployeeId bos qaldi", sql, StringComparison.Ordinal);
        Assert.Contains("hec ne silinmedi", sql, StringComparison.Ordinal);
        Assert.Contains("Naməlum işçi (köçürülmüş)", sql, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- plumbing

    private static string ConnectionString(string database) =>
        $"Server=localhost;Database={database};Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    /// <summary>Drops the database and brings it back up to the migration just before the split.</summary>
    private static async Task<AuthDbContext> FreshAsync(string database)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer(ConnectionString(database), sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema)
                .CommandTimeout(120))
            .Options;

        var db = new AuthDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.GetService<IMigrator>().MigrateAsync(Before);
        return db;
    }

    private static async Task<Guid> InsertUserAsync(
        AuthDbContext db, Guid tenantId, string name, string phone, string role,
        decimal monthlySalary = 0m, bool isActive = true)
    {
        Guid id = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [identity].[Users]
                ([Id],[FullName],[Phone],[Email],[PasswordHash],[Role],[IsActive],[MonthlySalary],
                 [TenantId],[CreatedAt],[UpdatedAt])
            VALUES ({0}, {1}, {2}, NULL, N'hash', {3}, {4}, {5}, {6}, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            id, name, phone, role, isActive, monthlySalary, tenantId);

        return id;
    }

    private static async Task<Guid> InsertEntryAsync(
        AuthDbContext db, Guid tenantId, Guid userId, string type, decimal amount, string month,
        Guid createdByUserId, string? note = null)
    {
        Guid id = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [identity].[SalaryEntries]
                ([Id],[UserId],[Type],[Amount],[Note],[Date],[Month],[CreatedByUserId],
                 [TenantId],[CreatedAt],[UpdatedAt])
            VALUES ({0}, {1}, {2}, {3}, {4}, SYSUTCDATETIME(), {5}, {6}, {7},
                    SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            id, userId, type, amount,
            new SqlParameter("note", System.Data.SqlDbType.NVarChar, 500) { Value = (object?)note ?? DBNull.Value },
            month, createdByUserId, tenantId);

        return id;
    }

    private sealed record EmployeeRow(
        Guid Id, Guid TenantId, string? Phone, string Position, decimal MonthlySalary, bool IsActive);

    private static async Task<EmployeeRow> EmployeeAsync(AuthDbContext db, string fullName)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            """
            SELECT [Id],[TenantId],[Phone],[Position],[MonthlySalary],[IsActive]
            FROM   [identity].[Employees]
            WHERE  [FullName] = @name
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = fullName;
        command.Parameters.Add(parameter);

        await db.Database.OpenConnectionAsync();
        try
        {
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), $"'{fullName}' üçün Employee sətri tapılmadı");
            return new EmployeeRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.GetBoolean(5));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private sealed record EntryRow(
        Guid EmployeeId, string Type, decimal Amount, string Month, string? Note, Guid? CreatedByUserId);

    private static async Task<EntryRow> EntryAsync(AuthDbContext db, Guid id)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            """
            SELECT [EmployeeId],[Type],[Amount],[Month],[Note],[CreatedByUserId]
            FROM   [identity].[SalaryEntries]
            WHERE  [Id] = @id
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);

        await db.Database.OpenConnectionAsync();
        try
        {
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), $"Maaş sətri tapılmadı: {id}");
            return new EntryRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> ColumnExistsAsync(AuthDbContext db, string table, string column) =>
        await ScalarAsync<int>(db,
            $"""
             SELECT COUNT(*) FROM sys.columns
             WHERE object_id = OBJECT_ID('[identity].[{table}]') AND name = '{column}'
             """) > 0;

    private static async Task<T> ScalarAsync<T>(AuthDbContext db, string sql)
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
