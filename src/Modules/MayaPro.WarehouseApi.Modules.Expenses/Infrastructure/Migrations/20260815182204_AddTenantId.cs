using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Expenses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExpenseTypes_Name",
                schema: "expenses",
                table: "ExpenseTypes");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "expenses",
                table: "ExpenseTypes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "expenses",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // BE#35 — back-fill: every pre-multi-tenancy row belongs to the default shop
            // (TenantDefaults.DefaultTenantId, created by the Tenancy module's InitialTenancy migration).
            // A pure UPDATE of the freshly added column — no row is deleted or duplicated. Run before the
            // unique index below so it is built on the final values. Idempotent via the WHERE clause.
            migrationBuilder.Sql(
                """
                UPDATE [expenses].[Expenses]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';

                UPDATE [expenses].[ExpenseTypes]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseTypes_TenantId",
                schema: "expenses",
                table: "ExpenseTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseTypes_TenantId_Name",
                schema: "expenses",
                table: "ExpenseTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_TenantId",
                schema: "expenses",
                table: "Expenses",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExpenseTypes_TenantId",
                schema: "expenses",
                table: "ExpenseTypes");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseTypes_TenantId_Name",
                schema: "expenses",
                table: "ExpenseTypes");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_TenantId",
                schema: "expenses",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "expenses",
                table: "ExpenseTypes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "expenses",
                table: "Expenses");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseTypes_Name",
                schema: "expenses",
                table: "ExpenseTypes",
                column: "Name",
                unique: true);
        }
    }
}
