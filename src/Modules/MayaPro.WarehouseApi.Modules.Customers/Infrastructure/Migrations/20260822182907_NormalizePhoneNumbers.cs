using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Customers.Infrastructure.Migrations
{
    /// <summary>
    /// BE#46 — data-only migration: rewrites existing <c>customers.Customers.Phone</c> values into the
    /// canonical <c>994XXXXXXXXX</c> form the application now writes (see <c>PhoneNormalizer</c>). No schema
    /// changes; no row is inserted or deleted.
    /// <para>
    /// The T-SQL reproduces the C# rule exactly — strip to ASCII digits, then accept ten digits starting with
    /// <c>0</c> (leading zero replaced by <c>994</c>) or twelve digits starting with <c>994</c>. Anything else
    /// is left untouched: a value nobody can interpret is worth more to its owner than a guess, and the count
    /// of such rows is printed so an operator can go and look at them.
    /// </para>
    /// <para>
    /// Idempotent: the <c>UPDATE</c> only touches rows whose stored value differs from its canonical form
    /// (compared under a binary collation, so a stray trailing space counts as a difference), so a second run
    /// changes nothing.
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

                IF OBJECT_ID('tempdb..#CustomerPhones') IS NOT NULL DROP TABLE #CustomerPhones;

                CREATE TABLE #CustomerPhones
                (
                    Id        uniqueidentifier NOT NULL PRIMARY KEY,
                    OldPhone  nvarchar(30)     NULL,
                    Digits    nvarchar(60)     NOT NULL,
                    Canonical nvarchar(30)     NULL
                );

                -- Digits-only, order preserved. There is no regex in T-SQL, so each character position is
                -- tested against the ASCII range under a binary collation (the same "ASCII digits only" rule
                -- the C# side applies) and the survivors are concatenated with FOR XML PATH.
                WITH Positions AS
                (
                    SELECT TOP (60) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Position
                    FROM sys.all_objects
                )
                INSERT INTO #CustomerPhones (Id, OldPhone, Digits)
                SELECT  c.[Id],
                        c.[Phone],
                        ISNULL(
                        (
                            SELECT SUBSTRING(c.[Phone], p.Position, 1) AS [text()]
                            FROM Positions p
                            WHERE p.Position <= LEN(c.[Phone])
                              AND SUBSTRING(c.[Phone], p.Position, 1) COLLATE Latin1_General_BIN2 LIKE '[0-9]'
                            ORDER BY p.Position
                            FOR XML PATH('')
                        ), N'')
                FROM [customers].[Customers] c;

                UPDATE #CustomerPhones
                SET    Canonical = CASE
                           WHEN LEN(Digits) = 10 AND LEFT(Digits, 1) = N'0'   THEN N'994' + SUBSTRING(Digits, 2, 9)
                           WHEN LEN(Digits) = 12 AND LEFT(Digits, 3) = N'994' THEN Digits
                       END;

                DECLARE @normalized int, @unconvertible int;

                UPDATE  c
                SET     c.[Phone] = p.Canonical
                FROM    [customers].[Customers] c
                JOIN    #CustomerPhones p ON p.Id = c.[Id]
                WHERE   p.Canonical IS NOT NULL
                  AND   c.[Phone] COLLATE Latin1_General_BIN2 <> p.Canonical COLLATE Latin1_General_BIN2;

                SET @normalized = @@ROWCOUNT;

                -- "Could not convert" counts values that are present but unreadable. NULL and blank phones are
                -- not failures: the column is optional and staying empty is the correct outcome for them.
                SELECT @unconvertible = COUNT(*)
                FROM   #CustomerPhones
                WHERE  Canonical IS NULL AND LEN(ISNULL(OldPhone, N'')) > 0;

                DECLARE @log nvarchar(200) = CONCAT(
                    N'[BE#46] customers.Customers.Phone - normallasdirildi: ', @normalized,
                    N', cevrile bilmedi: ', @unconvertible);
                RAISERROR(@log, 0, 1) WITH NOWAIT;

                DROP TABLE #CustomerPhones;
                """);
        }

        /// <summary>
        /// Schema-safe no-op. This migration changed no schema, so there is nothing to undo — and the old,
        /// inconsistently formatted phone strings are deliberately <b>not</b> restored: they were never
        /// recorded anywhere, and reverting to them would break the login of anyone who signed in since.
        /// Rolling back is safe; it simply leaves the phones canonical.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
