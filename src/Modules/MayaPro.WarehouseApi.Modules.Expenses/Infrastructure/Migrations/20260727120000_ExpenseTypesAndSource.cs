using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Expenses.Infrastructure.Migrations
{
    /// <summary>
    /// Two structural changes to the expense model, applied without data loss:
    /// <list type="number">
    /// <item>a managed <c>ExpenseTypes</c> table (the pick-list the UI offers) — <c>Expense.Category</c>
    /// stops being a fixed enum and becomes a free-form snapshot string, widened to fit any type name;
    /// existing rows are rewritten from their old enum member name to the matching Azerbaijani type name
    /// (e.g. <c>Transport</c> → <c>Yol pulu</c>);</item>
    /// <item>a new <c>Source</c> column ("general" | "product") backfilled from whether the row already
    /// carries a <c>ProductId</c> — a product-linked row is <c>product</c>, everything else <c>general</c>.</item>
    /// </list>
    /// </summary>
    public partial class ExpenseTypesAndSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Category was persisted as the ExpenseCategory enum's name (max 20 chars); it is now a free-form
            //    snapshot that can be any managed type's name (or arbitrary text), so widen it to match
            //    ExpenseTypes.Name.
            migrationBuilder.AlterColumn<string>(
                name: "Category",
                schema: "expenses",
                table: "Expenses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            // 2. Rewrite the old enum member names to the Azerbaijani names the managed ExpenseTypes seed
            //    uses, so existing rows read naturally and match a real type going forward.
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Category] = N'Yol pulu' WHERE [Category] = N'Transport';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Category] = N'Fəhlə pulu' WHERE [Category] = N'Labor';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Category] = N'Yer/Anbar xərci' WHERE [Category] = N'Storage';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Category] = N'Paket/Qutu' WHERE [Category] = N'Packaging';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Category] = N'Mağaza xərci' WHERE [Category] = N'Store';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Category] = N'Digər' WHERE [Category] = N'Other';");

            // 3. New Source column — added nullable so the backfill decides every row explicitly. It is NOT
            //    added with a SQL default: that would leave a permanent DEFAULT constraint on the table which
            //    the EF model knows nothing about (schema drift on the next scaffolded migration).
            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "expenses",
                table: "Expenses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // 4. Backfill, both branches spelled out so no row can be left NULL: a row already linked to a
            //    product was always product-sourced, everything else is a general store expense. Both
            //    statements are re-runnable (idempotent) and together cover every row exactly once.
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Source] = N'product' WHERE [ProductId] IS NOT NULL;");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Source] = N'general' WHERE [ProductId] IS NULL;");

            // Every row now carries a value → make the column required, matching the entity model.
            migrationBuilder.AlterColumn<string>(
                name: "Source",
                schema: "expenses",
                table: "Expenses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            // 5. Managed expense type list (seeded separately by ExpenseTypeSeeder, Development only).
            migrationBuilder.CreateTable(
                name: "ExpenseTypes",
                schema: "expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseTypes_Name",
                schema: "expenses",
                table: "ExpenseTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpenseTypes",
                schema: "expenses");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "expenses",
                table: "Expenses");

            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Category] = N'Transport' WHERE [Category] = N'Yol pulu';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Category] = N'Labor' WHERE [Category] = N'Fəhlə pulu';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Category] = N'Storage' WHERE [Category] = N'Yer/Anbar xərci';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Category] = N'Packaging' WHERE [Category] = N'Paket/Qutu';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Category] = N'Store' WHERE [Category] = N'Mağaza xərci';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET [Category] = N'Other' WHERE [Category] = N'Digər';");

            // Rows recorded after this migration can carry any managed type's name (e.g. "Sığorta", or one
            // longer than the old 20-char column). Fold anything that is not a legacy enum member into
            // 'Other' so shrinking the column cannot fail on truncation and the old enum-backed code can
            // still read every row. Down is lossy by nature — this makes it at least safe.
            migrationBuilder.Sql(
                "UPDATE [expenses].[Expenses] SET [Category] = N'Other' " +
                "WHERE [Category] NOT IN (N'Transport', N'Labor', N'Storage', N'Packaging', N'Store', N'Other');");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                schema: "expenses",
                table: "Expenses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
