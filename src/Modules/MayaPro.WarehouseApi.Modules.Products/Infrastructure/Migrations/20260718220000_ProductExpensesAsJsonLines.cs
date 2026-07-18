using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Products.Infrastructure.Migrations
{
    /// <summary>
    /// Replaces the fixed <c>Expenses_Transport</c>…<c>Expenses_Other</c> columns with a free-form JSON
    /// <c>Expenses</c> array of <c>[{"name":"…","amount":…}]</c>. Non-zero bucket values are copied with
    /// Azerbaijani line names before the old columns are dropped; <see cref="Down"/> reverses the mapping
    /// (recognized names → columns, everything else summed into Other).
    /// </summary>
    public partial class ProductExpensesAsJsonLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Expenses",
                schema: "products",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            // Copy non-zero buckets into JSON lines. FORMAT(..., 'en-US') keeps a culture-invariant decimal.
            migrationBuilder.Sql(
                """
                UPDATE [products].[Products]
                SET [Expenses] = N'[' + STUFF(
                        CASE WHEN [Expenses_Transport] <> 0 THEN N',{"name":"Yol pulu","amount":'        + FORMAT([Expenses_Transport], '0.##########', 'en-US') + N'}' ELSE N'' END +
                        CASE WHEN [Expenses_Labor]     <> 0 THEN N',{"name":"Fəhlə pulu","amount":'       + FORMAT([Expenses_Labor],     '0.##########', 'en-US') + N'}' ELSE N'' END +
                        CASE WHEN [Expenses_Storage]   <> 0 THEN N',{"name":"Yer/Anbar xərci","amount":'  + FORMAT([Expenses_Storage],   '0.##########', 'en-US') + N'}' ELSE N'' END +
                        CASE WHEN [Expenses_Packaging] <> 0 THEN N',{"name":"Paket/Qutu","amount":'       + FORMAT([Expenses_Packaging], '0.##########', 'en-US') + N'}' ELSE N'' END +
                        CASE WHEN [Expenses_Other]     <> 0 THEN N',{"name":"Digər","amount":'            + FORMAT([Expenses_Other],     '0.##########', 'en-US') + N'}' ELSE N'' END,
                        1, 1, N'')
                    + N']'
                WHERE [Expenses_Transport] <> 0
                   OR [Expenses_Labor]     <> 0
                   OR [Expenses_Storage]   <> 0
                   OR [Expenses_Packaging] <> 0
                   OR [Expenses_Other]     <> 0;
                """);

            migrationBuilder.DropColumn(name: "Expenses_Transport", schema: "products", table: "Products");
            migrationBuilder.DropColumn(name: "Expenses_Labor", schema: "products", table: "Products");
            migrationBuilder.DropColumn(name: "Expenses_Storage", schema: "products", table: "Products");
            migrationBuilder.DropColumn(name: "Expenses_Packaging", schema: "products", table: "Products");
            migrationBuilder.DropColumn(name: "Expenses_Other", schema: "products", table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Expenses_Transport",
                schema: "products",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Expenses_Labor",
                schema: "products",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Expenses_Storage",
                schema: "products",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Expenses_Packaging",
                schema: "products",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Expenses_Other",
                schema: "products",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Recognized Azerbaijani names → their columns; any other line name sums into Other.
            migrationBuilder.Sql(
                """
                UPDATE p
                SET
                    [Expenses_Transport] = ISNULL((
                        SELECT SUM(j.[amount]) FROM OPENJSON(p.[Expenses])
                        WITH ([name] nvarchar(200) '$.name', [amount] decimal(18,2) '$.amount') j
                        WHERE j.[name] = N'Yol pulu'), 0),
                    [Expenses_Labor] = ISNULL((
                        SELECT SUM(j.[amount]) FROM OPENJSON(p.[Expenses])
                        WITH ([name] nvarchar(200) '$.name', [amount] decimal(18,2) '$.amount') j
                        WHERE j.[name] = N'Fəhlə pulu'), 0),
                    [Expenses_Storage] = ISNULL((
                        SELECT SUM(j.[amount]) FROM OPENJSON(p.[Expenses])
                        WITH ([name] nvarchar(200) '$.name', [amount] decimal(18,2) '$.amount') j
                        WHERE j.[name] = N'Yer/Anbar xərci'), 0),
                    [Expenses_Packaging] = ISNULL((
                        SELECT SUM(j.[amount]) FROM OPENJSON(p.[Expenses])
                        WITH ([name] nvarchar(200) '$.name', [amount] decimal(18,2) '$.amount') j
                        WHERE j.[name] = N'Paket/Qutu'), 0),
                    [Expenses_Other] = ISNULL((
                        SELECT SUM(j.[amount]) FROM OPENJSON(p.[Expenses])
                        WITH ([name] nvarchar(200) '$.name', [amount] decimal(18,2) '$.amount') j
                        WHERE j.[name] = N'Digər'
                           OR j.[name] NOT IN (N'Yol pulu', N'Fəhlə pulu', N'Yer/Anbar xərci', N'Paket/Qutu', N'Digər')), 0)
                FROM [products].[Products] p
                WHERE p.[Expenses] IS NOT NULL AND p.[Expenses] <> N'[]';
                """);

            migrationBuilder.DropColumn(name: "Expenses", schema: "products", table: "Products");
        }
    }
}
