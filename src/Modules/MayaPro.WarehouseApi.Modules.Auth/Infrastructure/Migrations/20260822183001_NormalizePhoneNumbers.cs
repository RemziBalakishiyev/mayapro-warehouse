using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure.Migrations
{
    /// <summary>
    /// BE#46 — data-only migration for <c>identity.Users.Phone</c>. This is the one that matters most: the
    /// phone <b>is</b> the login identifier, and <c>IX_Users_TenantId_Phone</c> makes it unique inside a shop.
    /// After this runs, that index enforces uniqueness over canonical values, so <c>0501234567</c> and
    /// <c>+994 50 123 45 67</c> stop being two different accounts in the same shop.
    /// <para>
    /// <b>Order is the whole design.</b> The canonical value of every row is computed into a temp table first
    /// and checked for collisions <i>before</i> a single row is written. Normalizing first and discovering the
    /// duplicate on the way in would surface as an opaque unique-index violation halfway through the update —
    /// with no way to tell the operator which accounts collided. Instead the migration throws with the
    /// tenant, the canonical number and every <c>User.Id</c>/name involved, and the surrounding transaction
    /// rolls back leaving the table exactly as it was. Start-up then fails loudly, which is the correct
    /// outcome: two people who would both answer to the same login is not a state to boot into. Fix the rows
    /// by hand and run again.
    /// </para>
    /// <para>
    /// Everything else matches the other four <c>NormalizePhoneNumbers</c> migrations: the T-SQL mirrors
    /// <c>PhoneNormalizer</c>, unreadable values are left alone and counted, and the update is idempotent.
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

                IF OBJECT_ID('tempdb..#UserPhones') IS NOT NULL DROP TABLE #UserPhones;
                IF OBJECT_ID('tempdb..#UserPhoneCollisions') IS NOT NULL DROP TABLE #UserPhoneCollisions;

                CREATE TABLE #UserPhones
                (
                    Id        uniqueidentifier NOT NULL PRIMARY KEY,
                    TenantId  uniqueidentifier NOT NULL,
                    FullName  nvarchar(200)    NULL,
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
                INSERT INTO #UserPhones (Id, TenantId, FullName, OldPhone, Digits)
                SELECT  u.[Id],
                        u.[TenantId],
                        u.[FullName],
                        u.[Phone],
                        ISNULL(
                        (
                            SELECT SUBSTRING(u.[Phone], p.Position, 1) AS [text()]
                            FROM Positions p
                            WHERE p.Position <= LEN(u.[Phone])
                              AND SUBSTRING(u.[Phone], p.Position, 1) COLLATE Latin1_General_BIN2 LIKE '[0-9]'
                            ORDER BY p.Position
                            FOR XML PATH('')
                        ), N'')
                FROM [identity].[Users] u;

                UPDATE #UserPhones
                SET    Canonical = CASE
                           WHEN LEN(Digits) = 10 AND LEFT(Digits, 1) = N'0'   THEN N'994' + SUBSTRING(Digits, 2, 9)
                           WHEN LEN(Digits) = 12 AND LEFT(Digits, 3) = N'994' THEN Digits
                       END;

                -- STEP 1 — collisions, BEFORE anything is written. Scoped by TenantId because that is exactly
                -- what IX_Users_TenantId_Phone covers: the same number in two different shops is legitimate.
                SELECT   TenantId, Canonical
                INTO     #UserPhoneCollisions
                FROM     #UserPhones
                WHERE    Canonical IS NOT NULL
                GROUP BY TenantId, Canonical
                HAVING   COUNT(*) > 1;

                IF EXISTS (SELECT 1 FROM #UserPhoneCollisions)
                BEGIN
                    DECLARE @rows nvarchar(max) =
                    (
                        SELECT CHAR(13) + CHAR(10)
                             + N'  TenantId=' + CONVERT(nvarchar(36), u.TenantId)
                             + N' telefon=' + u.Canonical
                             + N' UserId=' + CONVERT(nvarchar(36), u.Id)
                             + N' (' + ISNULL(u.FullName, N'?') + N', kohne: ' + ISNULL(u.OldPhone, N'') + N')'
                        FROM #UserPhones u
                        JOIN #UserPhoneCollisions c
                          ON c.TenantId = u.TenantId AND c.Canonical = u.Canonical
                        ORDER BY u.TenantId, u.Canonical, u.Id
                        FOR XML PATH(''), TYPE
                    ).value('.', 'nvarchar(max)');

                    -- THROW's message is nvarchar(2048); a very long list is cut rather than losing the
                    -- explanation that precedes it.
                    DECLARE @error nvarchar(2048) = LEFT(
                        N'[BE#46] identity.Users.Phone normallasdirilmadi. Eyni magazada (TenantId) eyni '
                      + N'kanonik telefona dusen 2 ve ya daha cox istifadeci var, ona gore migration '
                      + N'dayandirildi ve hec bir setir deyismedi. Asagidaki setirleri elle duzeldin '
                      + N'(telefonu deyisin ve ya artiq hesabi deaktiv edin), sonra migration-i yeniden '
                      + N'isledin.' + ISNULL(@rows, N''), 2048);

                    THROW 50046, @error, 1;
                END;

                -- STEP 2 — only now, with uniqueness guaranteed, rewrite the rows.
                DECLARE @normalized int, @unconvertible int;

                UPDATE  u
                SET     u.[Phone] = p.Canonical
                FROM    [identity].[Users] u
                JOIN    #UserPhones p ON p.Id = u.[Id]
                WHERE   p.Canonical IS NOT NULL
                  AND   u.[Phone] COLLATE Latin1_General_BIN2 <> p.Canonical COLLATE Latin1_General_BIN2;

                SET @normalized = @@ROWCOUNT;

                -- Values that are present but unreadable stay exactly as they are; they are only counted, so
                -- an operator knows to go and look. (Users.Phone is required, so blanks are not expected.)
                SELECT @unconvertible = COUNT(*)
                FROM   #UserPhones
                WHERE  Canonical IS NULL AND LEN(ISNULL(OldPhone, N'')) > 0;

                DECLARE @log nvarchar(200) = CONCAT(
                    N'[BE#46] identity.Users.Phone - normallasdirildi: ', @normalized,
                    N', cevrile bilmedi: ', @unconvertible);
                RAISERROR(@log, 0, 1) WITH NOWAIT;

                DROP TABLE #UserPhoneCollisions;
                DROP TABLE #UserPhones;
                """);
        }

        /// <summary>
        /// Schema-safe no-op. Nothing was added to undo — <c>IX_Users_TenantId_Phone</c> was neither dropped
        /// nor recreated, only its contents changed — and the old, inconsistently formatted phone strings are
        /// deliberately <b>not</b> restored: they were never recorded anywhere, and reverting to them would
        /// break the login of everyone who has signed in since.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
