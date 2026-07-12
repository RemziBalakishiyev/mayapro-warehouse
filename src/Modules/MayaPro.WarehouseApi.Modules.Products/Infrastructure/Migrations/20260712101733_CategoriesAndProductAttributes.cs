using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Products.Infrastructure.Migrations
{
    /// <summary>
    /// Two structural changes to the product model, applied without data loss:
    /// <list type="number">
    /// <item>a managed <c>Categories</c> table, seeded from the distinct category names already on products;</item>
    /// <item>the fixed <c>Size</c>/<c>Color</c>/<c>Model</c> columns are replaced by a single JSON
    /// <c>Attributes</c> array — existing values are copied across (only the non-empty ones) as
    /// <c>[{"name":"Ölçü",...},{"name":"Rəng",...},{"name":"Model",...}]</c> before the old columns are dropped.</item>
    /// </list>
    /// </summary>
    public partial class CategoriesAndProductAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. New Attributes column, defaulting to an empty JSON array so pre-existing rows stay valid JSON.
            migrationBuilder.AddColumn<string>(
                name: "Attributes",
                schema: "products",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            // 2. Managed category list.
            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                schema: "products",
                table: "Categories",
                column: "Name",
                unique: true);

            // 3. Copy Size/Color/Model into the JSON Attributes array — only the non-empty values, in a fixed
            //    order, with STRING_ESCAPE guarding any quotes/backslashes in the stored text. Rows where all
            //    three are empty keep the default "[]". STUFF strips the leading comma from the concatenation.
            migrationBuilder.Sql(
                """
                UPDATE [products].[Products]
                SET [Attributes] = N'[' + STUFF(
                        CASE WHEN [Size]  <> N'' THEN N',{"name":"Ölçü","value":"'  + STRING_ESCAPE([Size],  'json') + N'"}' ELSE N'' END +
                        CASE WHEN [Color] <> N'' THEN N',{"name":"Rəng","value":"'  + STRING_ESCAPE([Color], 'json') + N'"}' ELSE N'' END +
                        CASE WHEN [Model] <> N'' THEN N',{"name":"Model","value":"' + STRING_ESCAPE([Model], 'json') + N'"}' ELSE N'' END,
                        1, 1, N'')
                    + N']'
                WHERE [Size] <> N'' OR [Color] <> N'' OR [Model] <> N'';
                """);

            // 4. Seed Categories from the distinct non-empty category names already on products. DISTINCT and the
            //    unique index share SQL Server's default (case-insensitive) collation, so no duplicate-key clash.
            migrationBuilder.Sql(
                """
                INSERT INTO [products].[Categories] ([Id], [Name], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), x.[Category], SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM (SELECT DISTINCT [Category] FROM [products].[Products] WHERE [Category] <> N'') x
                WHERE NOT EXISTS (SELECT 1 FROM [products].[Categories] c WHERE c.[Name] = x.[Category]);
                """);

            // 5. Old fixed columns are now redundant.
            migrationBuilder.DropColumn(name: "Size", schema: "products", table: "Products");
            migrationBuilder.DropColumn(name: "Color", schema: "products", table: "Products");
            migrationBuilder.DropColumn(name: "Model", schema: "products", table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate the fixed columns (empty by default)...
            migrationBuilder.AddColumn<string>(
                name: "Size",
                schema: "products",
                table: "Products",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Color",
                schema: "products",
                table: "Products",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Model",
                schema: "products",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // ...then pull the values back out of the JSON by attribute name (best-effort restore).
            migrationBuilder.Sql(
                """
                UPDATE p SET
                    [Size]  = COALESCE(sz.[value], N''),
                    [Color] = COALESCE(cl.[value], N''),
                    [Model] = COALESCE(md.[value], N'')
                FROM [products].[Products] p
                OUTER APPLY (SELECT TOP 1 a.[value] FROM OPENJSON(p.[Attributes])
                    WITH ([name] nvarchar(100) '$.name', [value] nvarchar(max) '$.value') a
                    WHERE a.[name] = N'Ölçü') sz
                OUTER APPLY (SELECT TOP 1 a.[value] FROM OPENJSON(p.[Attributes])
                    WITH ([name] nvarchar(100) '$.name', [value] nvarchar(max) '$.value') a
                    WHERE a.[name] = N'Rəng') cl
                OUTER APPLY (SELECT TOP 1 a.[value] FROM OPENJSON(p.[Attributes])
                    WITH ([name] nvarchar(100) '$.name', [value] nvarchar(max) '$.value') a
                    WHERE a.[name] = N'Model') md;
                """);

            migrationBuilder.DropTable(name: "Categories", schema: "products");

            migrationBuilder.DropColumn(name: "Attributes", schema: "products", table: "Products");
        }
    }
}
