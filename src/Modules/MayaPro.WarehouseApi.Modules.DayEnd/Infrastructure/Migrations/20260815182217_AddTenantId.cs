using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.DayEnd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Closings_Date",
                schema: "dayend",
                table: "Closings");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "dayend",
                table: "Closings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // BE#35 — back-fill: every pre-multi-tenancy closing belongs to the default shop
            // (TenantDefaults.DefaultTenantId, created by the Tenancy module's InitialTenancy migration).
            // A pure UPDATE of the freshly added column — no closing is deleted or duplicated. Run before
            // the (TenantId, Date) unique index below. Idempotent via the WHERE clause.
            migrationBuilder.Sql(
                """
                UPDATE [dayend].[Closings]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Closings_TenantId",
                schema: "dayend",
                table: "Closings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Closings_TenantId_Date",
                schema: "dayend",
                table: "Closings",
                columns: new[] { "TenantId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Closings_TenantId",
                schema: "dayend",
                table: "Closings");

            migrationBuilder.DropIndex(
                name: "IX_Closings_TenantId_Date",
                schema: "dayend",
                table: "Closings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "dayend",
                table: "Closings");

            migrationBuilder.CreateIndex(
                name: "IX_Closings_Date",
                schema: "dayend",
                table: "Closings",
                column: "Date",
                unique: true);
        }
    }
}
