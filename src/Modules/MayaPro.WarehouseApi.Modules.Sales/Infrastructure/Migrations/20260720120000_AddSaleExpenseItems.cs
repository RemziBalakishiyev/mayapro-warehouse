using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleExpenseItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Free-form expense lines stored inline as a JSON array. Existing rows (and catalogued sales)
            // default to an empty array.
            migrationBuilder.AddColumn<string>(
                name: "ExpenseItems",
                schema: "sales",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpenseItems",
                schema: "sales",
                table: "Sales");
        }
    }
}
