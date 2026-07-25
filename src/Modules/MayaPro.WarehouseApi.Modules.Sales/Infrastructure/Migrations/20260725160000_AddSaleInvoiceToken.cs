using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleInvoiceToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceToken",
                schema: "sales",
                table: "Sales",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_InvoiceToken",
                schema: "sales",
                table: "Sales",
                column: "InvoiceToken",
                unique: true,
                filter: "[InvoiceToken] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_InvoiceToken",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "InvoiceToken",
                schema: "sales",
                table: "Sales");
        }
    }
}
