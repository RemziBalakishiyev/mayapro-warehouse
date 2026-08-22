using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure.Migrations
{
    /// <summary>
    /// BE#46 — data-only migration for <c>tenancy.Tenants.Phone</c> (the shop owner's contact number; the
    /// login identifier itself lives in <c>identity.Users</c> and is normalized by the Auth migration of the
    /// same name). Same rule, same guarantees and the same log line as the Customers counterpart; see that
    /// migration for the reasoning behind the T-SQL.
    /// <para>
    /// No duplicate guard is needed here: no unique index covers this column — two shops may legitimately
    /// list the same contact number.
    /// </para>
    /// </summary>
    public partial class NormalizePhoneNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                IF OBJECT_ID('tempdb..#TenantPhones') IS NOT NULL DROP TABLE #TenantPhones;

                CREATE TABLE #TenantPhones
                (
                    Id        uniqueidentifier NOT NULL PRIMARY KEY,
                    OldPhone  nvarchar(30)     NULL,
                    Digits    nvarchar(60)     NOT NULL,
                    Canonical nvarchar(30)     NULL
                );

                WITH Positions AS
                (
                    SELECT TOP (60) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Position
                    FROM sys.all_objects
                )
                INSERT INTO #TenantPhones (Id, OldPhone, Digits)
                SELECT  t.[Id],
                        t.[Phone],
                        ISNULL(
                        (
                            SELECT SUBSTRING(t.[Phone], p.Position, 1) AS [text()]
                            FROM Positions p
                            WHERE p.Position <= LEN(t.[Phone])
                              AND SUBSTRING(t.[Phone], p.Position, 1) COLLATE Latin1_General_BIN2 LIKE '[0-9]'
                            ORDER BY p.Position
                            FOR XML PATH('')
                        ), N'')
                FROM [tenancy].[Tenants] t;

                UPDATE #TenantPhones
                SET    Canonical = CASE
                           WHEN LEN(Digits) = 10 AND LEFT(Digits, 1) = N'0'   THEN N'994' + SUBSTRING(Digits, 2, 9)
                           WHEN LEN(Digits) = 12 AND LEFT(Digits, 3) = N'994' THEN Digits
                       END;

                DECLARE @normalized int, @unconvertible int;

                UPDATE  t
                SET     t.[Phone] = p.Canonical
                FROM    [tenancy].[Tenants] t
                JOIN    #TenantPhones p ON p.Id = t.[Id]
                WHERE   p.Canonical IS NOT NULL
                  AND   t.[Phone] COLLATE Latin1_General_BIN2 <> p.Canonical COLLATE Latin1_General_BIN2;

                SET @normalized = @@ROWCOUNT;

                SELECT @unconvertible = COUNT(*)
                FROM   #TenantPhones
                WHERE  Canonical IS NULL AND LEN(ISNULL(OldPhone, N'')) > 0;

                DECLARE @log nvarchar(200) = CONCAT(
                    N'[BE#46] tenancy.Tenants.Phone - normallasdirildi: ', @normalized,
                    N', cevrile bilmedi: ', @unconvertible);
                RAISERROR(@log, 0, 1) WITH NOWAIT;

                DROP TABLE #TenantPhones;
                """);
        }

        /// <summary>
        /// Schema-safe no-op — nothing was added to undo, and the old free-form strings are deliberately not
        /// restored (they were never recorded). See the Customers migration for the full rationale.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
