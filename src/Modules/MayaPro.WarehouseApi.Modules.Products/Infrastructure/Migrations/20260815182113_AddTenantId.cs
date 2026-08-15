using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Products.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Barcode",
                schema: "products",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                schema: "products",
                table: "Categories");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "products",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "products",
                table: "Products",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "products",
                table: "Categories",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // BE#35 — back-fill: every pre-multi-tenancy row belongs to the default shop
            // (TenantDefaults.DefaultTenantId, created by the Tenancy module's InitialTenancy migration).
            // A pure UPDATE of the freshly added column — no row is deleted or duplicated. Run before the
            // unique indexes below so they are built on the final values. Idempotent via the WHERE clause.
            migrationBuilder.Sql(
                """
                UPDATE [products].[Products]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';

                UPDATE [products].[Categories]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';

                UPDATE [products].[StockAdjustments]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_TenantId",
                schema: "products",
                table: "StockAdjustments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId",
                schema: "products",
                table: "Products",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Barcode",
                schema: "products",
                table: "Products",
                columns: new[] { "TenantId", "Barcode" },
                unique: true,
                filter: "[Barcode] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_TenantId",
                schema: "products",
                table: "Categories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_TenantId_Name",
                schema: "products",
                table: "Categories",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockAdjustments_TenantId",
                schema: "products",
                table: "StockAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId",
                schema: "products",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_Barcode",
                schema: "products",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_TenantId",
                schema: "products",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_TenantId_Name",
                schema: "products",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "products",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "products",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "products",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                schema: "products",
                table: "Products",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                schema: "products",
                table: "Categories",
                column: "Name",
                unique: true);
        }
    }
}
