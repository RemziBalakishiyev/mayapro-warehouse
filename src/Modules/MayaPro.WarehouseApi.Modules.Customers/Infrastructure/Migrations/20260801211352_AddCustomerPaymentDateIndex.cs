using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Customers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPaymentDateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_Date",
                schema: "customers",
                table: "CustomerPayments",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_Date",
                schema: "customers",
                table: "CustomerPayments");
        }
    }
}
