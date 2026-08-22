using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Suppliers.Infrastructure.Migrations
{
    /// <summary>
    /// BE#46 — data-only migration for <c>suppliers.Suppliers.Phone</c>. Same rule, same guarantees and the
    /// same log line as the Customers counterpart; see that migration for the reasoning behind the T-SQL.
    /// </summary>
    public partial class NormalizePhoneNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                IF OBJECT_ID('tempdb..#SupplierPhones') IS NOT NULL DROP TABLE #SupplierPhones;

                CREATE TABLE #SupplierPhones
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
                INSERT INTO #SupplierPhones (Id, OldPhone, Digits)
                SELECT  s.[Id],
                        s.[Phone],
                        ISNULL(
                        (
                            SELECT SUBSTRING(s.[Phone], p.Position, 1) AS [text()]
                            FROM Positions p
                            WHERE p.Position <= LEN(s.[Phone])
                              AND SUBSTRING(s.[Phone], p.Position, 1) COLLATE Latin1_General_BIN2 LIKE '[0-9]'
                            ORDER BY p.Position
                            FOR XML PATH('')
                        ), N'')
                FROM [suppliers].[Suppliers] s;

                UPDATE #SupplierPhones
                SET    Canonical = CASE
                           WHEN LEN(Digits) = 10 AND LEFT(Digits, 1) = N'0'   THEN N'994' + SUBSTRING(Digits, 2, 9)
                           WHEN LEN(Digits) = 12 AND LEFT(Digits, 3) = N'994' THEN Digits
                       END;

                DECLARE @normalized int, @unconvertible int;

                UPDATE  s
                SET     s.[Phone] = p.Canonical
                FROM    [suppliers].[Suppliers] s
                JOIN    #SupplierPhones p ON p.Id = s.[Id]
                WHERE   p.Canonical IS NOT NULL
                  AND   s.[Phone] COLLATE Latin1_General_BIN2 <> p.Canonical COLLATE Latin1_General_BIN2;

                SET @normalized = @@ROWCOUNT;

                SELECT @unconvertible = COUNT(*)
                FROM   #SupplierPhones
                WHERE  Canonical IS NULL AND LEN(ISNULL(OldPhone, N'')) > 0;

                DECLARE @log nvarchar(200) = CONCAT(
                    N'[BE#46] suppliers.Suppliers.Phone - normallasdirildi: ', @normalized,
                    N', cevrile bilmedi: ', @unconvertible);
                RAISERROR(@log, 0, 1) WITH NOWAIT;

                DROP TABLE #SupplierPhones;
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
