using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Products.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameExpenseColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data-preserving column renames. Each pairing keeps the SAME meaning (Fehle=labour, Diger=other);
            // the values never move between buckets.
            migrationBuilder.RenameColumn(
                name: "Expenses_Yol",
                schema: "products",
                table: "Products",
                newName: "Expenses_Transport");

            migrationBuilder.RenameColumn(
                name: "Expenses_Fehle",
                schema: "products",
                table: "Products",
                newName: "Expenses_Labor");

            migrationBuilder.RenameColumn(
                name: "Expenses_Yer",
                schema: "products",
                table: "Products",
                newName: "Expenses_Storage");

            migrationBuilder.RenameColumn(
                name: "Expenses_Paket",
                schema: "products",
                table: "Products",
                newName: "Expenses_Packaging");

            migrationBuilder.RenameColumn(
                name: "Expenses_Diger",
                schema: "products",
                table: "Products",
                newName: "Expenses_Other");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Expenses_Transport",
                schema: "products",
                table: "Products",
                newName: "Expenses_Yol");

            migrationBuilder.RenameColumn(
                name: "Expenses_Labor",
                schema: "products",
                table: "Products",
                newName: "Expenses_Fehle");

            migrationBuilder.RenameColumn(
                name: "Expenses_Storage",
                schema: "products",
                table: "Products",
                newName: "Expenses_Yer");

            migrationBuilder.RenameColumn(
                name: "Expenses_Packaging",
                schema: "products",
                table: "Products",
                newName: "Expenses_Paket");

            migrationBuilder.RenameColumn(
                name: "Expenses_Other",
                schema: "products",
                table: "Products",
                newName: "Expenses_Diger");
        }
    }
}
