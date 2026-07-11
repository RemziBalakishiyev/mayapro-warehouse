using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Products.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "products");

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Image = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    InitialQuantity = table.Column<int>(type: "int", nullable: false),
                    MinStock = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SupplierId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Store = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Warehouse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Shelf = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Box = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Expenses_Yol = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Expenses_Fehle = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Expenses_Yer = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Expenses_Paket = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Expenses_Diger = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RealCostPerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                schema: "products",
                table: "Products",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products",
                schema: "products");
        }
    }
}
