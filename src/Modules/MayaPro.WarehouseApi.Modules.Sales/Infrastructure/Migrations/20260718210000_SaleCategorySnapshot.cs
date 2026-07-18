using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SaleCategorySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Category snapshot at sale time. Existing rows stay null; new catalogued sales write the
            // product's category, free-form sales may optionally supply one.
            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "sales",
                table: "Sales",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                schema: "sales",
                table: "Sales");
        }
    }
}
