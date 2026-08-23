using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure.Migrations
{
    /// <summary>
    /// BE#57 — splits the payroll register out of the login register. Until now one <c>identity.Users</c> row
    /// had to serve as both "someone who can sign in" and "someone we pay", which meant a labourer needed a
    /// password before his wage could be recorded. After this migration <c>identity.Employees</c> holds the
    /// payroll and <c>identity.Users</c> holds only logins.
    /// <para>
    /// <b>The order below is the whole design.</b> The salary history is real money that really left the
    /// drawer, and it is the one thing here that cannot be reconstructed if it is lost, so every destructive
    /// step happens <i>after</i> the copy has been proved complete:
    /// </para>
    /// <list type="number">
    /// <item><c>Employees</c> is created (empty);</item>
    /// <item><c>SalaryEntries.EmployeeId</c> is added <b>nullable</b> — deliberately, because "not mapped yet"
    /// has to be a state the verification in step 5 can see. SQL Server parses a whole batch before running
    /// it, so the column must exist as its own operation before any statement can name it;</item>
    /// <item>one <c>Employee</c> is created per user who is actually payroll (see the WHERE clause below) and
    /// every <c>SalaryEntry</c> is pointed at it through the <c>UserId → EmployeeId</c> map;</item>
    /// <item>lines whose user row no longer exists — there is no FK on <c>UserId</c>, so this is possible — are
    /// attached to a per-tenant "Naməlum işçi (köçürülmüş)" record instead of being dropped;</item>
    /// <item><b>verification:</b> if a single <c>EmployeeId IS NULL</c> survives, the migration THROWs. The
    /// surrounding transaction rolls back, <c>SalaryEntries.UserId</c> and <c>Users.MonthlySalary</c> are still
    /// there, and start-up fails loudly — which is the correct outcome, because booting into a half-migrated
    /// payroll would quietly change what the shop believes it has paid;</item>
    /// <item>only then are the old column, the old index and <c>Users.MonthlySalary</c> dropped.</item>
    /// </list>
    /// <para>
    /// <b>Who becomes an employee:</b> every non-Owner shop user, plus any user at all — Owner included — who
    /// has a salary line or a non-zero <c>MonthlySalary</c>. The second half of that rule exists so no existing
    /// figure can vanish: an owner who paid himself a salary keeps it. <c>PlatformAdmin</c> is excluded unless
    /// it too carries salary data: it is the platform operator, belongs to no shop, and is nobody's payroll.
    /// Login accounts themselves are <b>not</b> touched — no user is deleted or deactivated here.
    /// </para>
    /// <para>
    /// Row counts prove the point: <c>SalaryEntries</c> has exactly as many rows after this as before, with
    /// <c>Amount</c>, <c>Type</c>, <c>Date</c>, <c>Month</c>, <c>Note</c> and <c>CreatedByUserId</c> untouched.
    /// <c>CreatedByUserId</c> in particular stays a <b>user</b> id: "who was paid" (<c>EmployeeId</c>) and "who
    /// paid" (<c>CreatedByUserId</c>) are different questions and must never be merged.
    /// </para>
    /// </summary>
    public partial class SplitEmployeeFromUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- STEP 1: the new register, still empty ------------------------------------------------
            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    // Nullable and NOT unique — a contact number here, not a login identifier.
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Position = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId",
                schema: "identity",
                table: "Employees",
                column: "TenantId");

            // ---- STEP 2: the new link, nullable until it is proved complete ---------------------------
            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                schema: "identity",
                table: "SalaryEntries",
                type: "uniqueidentifier",
                nullable: true);

            // ---- STEPS 3-5: copy, rescue orphans, verify ----------------------------------------------
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                IF OBJECT_ID('tempdb..#Be57EmployeeMap') IS NOT NULL DROP TABLE #Be57EmployeeMap;
                IF OBJECT_ID('tempdb..#Be57Fallback') IS NOT NULL DROP TABLE #Be57Fallback;

                CREATE TABLE #Be57EmployeeMap
                (
                    UserId     uniqueidentifier NOT NULL PRIMARY KEY,
                    EmployeeId uniqueidentifier NOT NULL
                );

                -- Declared up front and assigned with SET immediately after the statement they count, so no
                -- intervening statement can reset @@ROWCOUNT underneath them.
                DECLARE @employees int, @mapped int, @fallbacks int, @rescued int, @unmapped int;

                -- STEP 3a — decide who is payroll, and hand each one a new Employee id up front. The id is
                -- generated here rather than by an OUTPUT clause so the same map can be used twice: once to
                -- insert the employees, once to re-point the salary lines.
                INSERT INTO #Be57EmployeeMap (UserId, EmployeeId)
                SELECT u.[Id], NEWID()
                FROM   [identity].[Users] u
                WHERE  (u.[Role] <> N'Owner' AND u.[Role] <> N'PlatformAdmin')
                   OR  u.[MonthlySalary] > 0
                   OR  EXISTS (SELECT 1 FROM [identity].[SalaryEntries] s WHERE s.[UserId] = u.[Id]);

                -- STEP 3b — the copy itself. TenantId, name, phone, agreed salary and active flag come over
                -- verbatim; CreatedAt is kept so the payroll list keeps the order people were added in.
                -- Position is the role rendered the way a shop owner writes it: it is free text from now on,
                -- never a role code (ADR-0006 codes stay on Users).
                INSERT INTO [identity].[Employees]
                    ([Id],[TenantId],[FullName],[Phone],[Position],[MonthlySalary],[Note],[IsActive],
                     [CreatedAt],[UpdatedAt])
                SELECT m.EmployeeId,
                       u.[TenantId],
                       u.[FullName],
                       u.[Phone],
                       CASE u.[Role]
                           WHEN N'Owner'   THEN N'Sahibkar'
                           WHEN N'Manager' THEN N'Menecer'
                           WHEN N'Seller'  THEN N'Satıcı'
                           ELSE N'İşçi'
                       END,
                       u.[MonthlySalary],
                       NULL,
                       u.[IsActive],
                       u.[CreatedAt],
                       SYSUTCDATETIME()
                FROM   #Be57EmployeeMap m
                JOIN   [identity].[Users] u ON u.[Id] = m.UserId;

                SET @employees = @@ROWCOUNT;

                -- STEP 4a — every salary line follows its person.
                UPDATE s
                SET    s.[EmployeeId] = m.EmployeeId
                FROM   [identity].[SalaryEntries] s
                JOIN   #Be57EmployeeMap m ON m.UserId = s.[UserId];

                SET @mapped = @@ROWCOUNT;

                -- STEP 4b — orphans. SalaryEntries.UserId never had a foreign key (a salary history has to
                -- outlive the person's account), so a line can point at a user row that is gone. Dropping
                -- those lines would silently reduce what the shop has spent, so each affected tenant gets one
                -- inactive "Naməlum işçi (köçürülmüş)" record to carry them.
                -- DISTINCT first, NEWID() after: NEWID() is evaluated per row, so a DISTINCT over it would
                -- never collapse anything and each orphan line would get its own "unknown employee".
                SELECT t.[TenantId], NEWID() AS EmployeeId
                INTO   #Be57Fallback
                FROM   (SELECT DISTINCT s.[TenantId]
                        FROM   [identity].[SalaryEntries] s
                        WHERE  s.[EmployeeId] IS NULL) t;

                INSERT INTO [identity].[Employees]
                    ([Id],[TenantId],[FullName],[Phone],[Position],[MonthlySalary],[Note],[IsActive],
                     [CreatedAt],[UpdatedAt])
                SELECT f.EmployeeId,
                       f.[TenantId],
                       N'Naməlum işçi (köçürülmüş)',
                       NULL,
                       N'Naməlum',
                       0,
                       N'BE#57 — bu setirlerin kohne istifadeci qeydi tapilmadi, maas tarixcesi itmesin deye bura baglanib.',
                       0,
                       SYSUTCDATETIME(),
                       SYSUTCDATETIME()
                FROM   #Be57Fallback f;

                SET @fallbacks = @@ROWCOUNT;

                UPDATE s
                SET    s.[EmployeeId] = f.EmployeeId
                FROM   [identity].[SalaryEntries] s
                JOIN   #Be57Fallback f ON f.[TenantId] = s.[TenantId]
                WHERE  s.[EmployeeId] IS NULL;

                SET @rescued = @@ROWCOUNT;

                -- STEP 5 — the gate. Nothing has been dropped yet, so failing here costs nothing.
                SET @unmapped =
                    (SELECT COUNT(*) FROM [identity].[SalaryEntries] WHERE [EmployeeId] IS NULL);

                IF @unmapped > 0
                BEGIN
                    DECLARE @error nvarchar(2048) = LEFT(
                        N'[BE#57] identity.SalaryEntries kocurulmedi: '
                      + CONVERT(nvarchar(20), @unmapped)
                      + N' setirde EmployeeId bos qaldi, ona gore migration dayandirildi ve hec ne silinmedi '
                      + N'(UserId sutunu ve Users.MonthlySalary yerindedir). Bu setirleri elle bir Employee '
                      + N'qeydine baglayin, sonra migration-i yeniden isledin.', 2048);

                    THROW 50057, @error, 1;
                END;

                DECLARE @log nvarchar(300) = CONCAT(
                    N'[BE#57] identity.Employees - kocurulen isci: ', @employees,
                    N', baglanan maas setiri: ', @mapped,
                    N', sahibsiz setir ucun yaradilan qeyd: ', @fallbacks,
                    N' (setir: ', @rescued, N')');
                RAISERROR(@log, 0, 1) WITH NOWAIT;

                DROP TABLE #Be57Fallback;
                DROP TABLE #Be57EmployeeMap;
                """);

            // ---- STEP 6: only now is anything destroyed ------------------------------------------------
            migrationBuilder.AlterColumn<Guid>(
                name: "EmployeeId",
                schema: "identity",
                table: "SalaryEntries",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_SalaryEntries_UserId_Month",
                schema: "identity",
                table: "SalaryEntries");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryEntries_EmployeeId_Month",
                schema: "identity",
                table: "SalaryEntries",
                columns: new[] { "EmployeeId", "Month" });

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "identity",
                table: "SalaryEntries");

            migrationBuilder.DropColumn(
                name: "MonthlySalary",
                schema: "identity",
                table: "Users");
        }

        /// <summary>
        /// Schema-only rollback. The columns and indexes come back exactly as they were, but their
        /// <b>contents</b> deliberately do not: <c>SalaryEntries.UserId</c> and <c>Users.MonthlySalary</c> were
        /// dropped after the data had been copied into <c>Employees</c>, and reversing that copy would mean
        /// guessing which login account each payroll record used to be — including for the rows this migration
        /// created from scratch, which never had a user at all. A rollback therefore leaves the salary lines
        /// pointing nowhere on purpose, which is visible and fixable, rather than pointing at the wrong person,
        /// which is not.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MonthlySalary",
                schema: "identity",
                table: "Users",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "identity",
                table: "SalaryEntries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.DropIndex(
                name: "IX_SalaryEntries_EmployeeId_Month",
                schema: "identity",
                table: "SalaryEntries");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryEntries_UserId_Month",
                schema: "identity",
                table: "SalaryEntries",
                columns: new[] { "UserId", "Month" });

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                schema: "identity",
                table: "SalaryEntries");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "identity");
        }
    }
}
