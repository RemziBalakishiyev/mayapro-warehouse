using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Products.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockAdjustments",
                schema: "products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Delta = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Date",
                schema: "products",
                table: "StockAdjustments",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_ProductId",
                schema: "products",
                table: "StockAdjustments",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockAdjustments",
                schema: "products");
        }
    }
}
