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
            // (PaidAmount = TotalAmount, PaidVia = its own payment type); a Credit (Nisyə) sale predates any
            // down-payment concept, so nothing was paid at sale time (PaidAmount = 0) — its PaidVia defaults
            // to Cash (harmless: zero paid means nothing is attributed to either cash or card).
            migrationBuilder.Sql(
                """
                UPDATE sales.Sales
                SET PaidAmount = CASE WHEN PaymentType = N'Credit' THEN 0 ELSE TotalAmount END,
                    PaidVia = CASE WHEN PaymentType = N'Card' THEN N'Card' ELSE N'Cash' END;
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
