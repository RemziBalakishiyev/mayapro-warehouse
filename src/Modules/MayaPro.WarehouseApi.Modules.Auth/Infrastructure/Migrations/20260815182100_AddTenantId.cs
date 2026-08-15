using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Phone",
                schema: "identity",
                table: "Users");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "identity",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "identity",
                table: "SalaryEntries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // BE#35 — back-fill: every pre-multi-tenancy row belongs to the default shop
            // (TenantDefaults.DefaultTenantId, created by the Tenancy module's InitialTenancy migration).
            // Nothing is deleted or duplicated: this is a pure UPDATE of the freshly added column, so row
            // counts before and after are identical. The WHERE clause makes it idempotent — a re-run finds
            // no Guid.Empty rows left and touches nothing.
            migrationBuilder.Sql(
                """
                UPDATE [identity].[Users]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';

                UPDATE [identity].[SalaryEntries]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                schema: "identity",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_Phone",
                schema: "identity",
                table: "Users",
                columns: new[] { "TenantId", "Phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryEntries_TenantId",
                schema: "identity",
                table: "SalaryEntries",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_Phone",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_SalaryEntries_TenantId",
                schema: "identity",
                table: "SalaryEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "identity",
                table: "SalaryEntries");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone",
                schema: "identity",
                table: "Users",
                column: "Phone",
                unique: true);
        }
    }
}
