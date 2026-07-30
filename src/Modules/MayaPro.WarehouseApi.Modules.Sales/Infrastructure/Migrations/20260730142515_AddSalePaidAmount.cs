using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalePaidAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                schema: "sales",
                table: "Sales",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaidVia",
                schema: "sales",
                table: "Sales",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Cash");

            // Backfill existing rows (BE#15 — qismən ödənişli satış) so the new columns reflect what every
            // pre-existing sale already was in full: a Cash/Card sale was always paid in full at sale time
            // (PaidAmount = TotalAmount, PaidVia = its own payment type). A Credit (Nisyə) sale predates any
            // down-payment concept, so nothing was paid at sale time — it already has exactly what the new
            // columns default to (0 / Cash, harmless since zero paid attributes nothing to either method), so
            // it is deliberately left untouched rather than rewritten.
            //
            // Re-run safe (see docs/database/DATABASE.md): the WHERE clause only matches rows that still carry
            // the freshly added default. A partially paid sale is always stored as Credit, and a stored
            // Cash/Card sale is by definition paid in full, so no row written after this migration can be
            // caught by it — replaying the statement can never wipe a real paid amount.
            migrationBuilder.Sql(
                """
                UPDATE sales.Sales
                SET PaidAmount = TotalAmount,
                    PaidVia = PaymentType
                WHERE PaymentType <> N'Credit' AND PaidAmount = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidAmount",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PaidVia",
                schema: "sales",
                table: "Sales");
        }
    }
}
