using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Suppliers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "suppliers",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "suppliers",
                table: "SupplierPayments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "suppliers",
                table: "SupplierDebtAdjustments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // BE#35 — back-fill: every pre-multi-tenancy row belongs to the default shop
            // (TenantDefaults.DefaultTenantId, created by the Tenancy module's InitialTenancy migration).
            // A pure UPDATE of the freshly added column — no row is deleted or duplicated, so counts match
            // exactly before and after. Idempotent via the WHERE clause.
            migrationBuilder.Sql(
                """
                UPDATE [suppliers].[Suppliers]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';

                UPDATE [suppliers].[SupplierPayments]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';

                UPDATE [suppliers].[SupplierDebtAdjustments]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId",
                schema: "suppliers",
                table: "Suppliers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_TenantId",
                schema: "suppliers",
                table: "SupplierPayments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDebtAdjustments_TenantId",
                schema: "suppliers",
                table: "SupplierDebtAdjustments",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Suppliers_TenantId",
                schema: "suppliers",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPayments_TenantId",
                schema: "suppliers",
                table: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_SupplierDebtAdjustments_TenantId",
                schema: "suppliers",
                table: "SupplierDebtAdjustments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "suppliers",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "suppliers",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "suppliers",
                table: "SupplierDebtAdjustments");
        }
    }
}
