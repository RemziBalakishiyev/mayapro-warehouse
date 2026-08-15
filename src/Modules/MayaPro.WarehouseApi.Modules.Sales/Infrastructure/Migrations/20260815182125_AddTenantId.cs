using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "sales",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // BE#35 — back-fill: every pre-multi-tenancy sale belongs to the default shop
            // (TenantDefaults.DefaultTenantId, created by the Tenancy module's InitialTenancy migration).
            // A pure UPDATE of the freshly added column — no sale is deleted or duplicated, and historic
            // profit/cost snapshots are untouched. Idempotent via the WHERE clause.
            // Note: Sales.InvoiceToken stays GLOBALLY unique — the anonymous invoice link resolves the
            // tenant from the token, so it must not be scoped by one.
            migrationBuilder.Sql(
                """
                UPDATE [sales].[Sales]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_TenantId",
                schema: "sales",
                table: "Sales",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_TenantId",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "sales",
                table: "Sales");
        }
    }
}
