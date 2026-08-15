using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Customers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "customers",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "customers",
                table: "CustomerPayments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "customers",
                table: "CustomerDebtAdjustments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // BE#35 — back-fill: every pre-multi-tenancy row belongs to the default shop
            // (TenantDefaults.DefaultTenantId, created by the Tenancy module's InitialTenancy migration).
            // A pure UPDATE of the freshly added column — no row is deleted or duplicated, so counts match
            // exactly before and after. Idempotent via the WHERE clause.
            migrationBuilder.Sql(
                """
                UPDATE [customers].[Customers]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';

                UPDATE [customers].[CustomerPayments]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';

                UPDATE [customers].[CustomerDebtAdjustments]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId",
                schema: "customers",
                table: "Customers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_TenantId",
                schema: "customers",
                table: "CustomerPayments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDebtAdjustments_TenantId",
                schema: "customers",
                table: "CustomerDebtAdjustments",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId",
                schema: "customers",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_TenantId",
                schema: "customers",
                table: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerDebtAdjustments_TenantId",
                schema: "customers",
                table: "CustomerDebtAdjustments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "customers",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "customers",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "customers",
                table: "CustomerDebtAdjustments");
        }
    }
}
