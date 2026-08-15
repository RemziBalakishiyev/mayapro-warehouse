using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Settings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "settings",
                table: "StoreSettings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // BE#35 — back-fill: the old singleton settings row (fixed id 1111…) becomes the default shop's
            // settings. Every store name, WhatsApp template and currency the shop had configured is
            // preserved as-is; only the owning tenant is filled in. Must run before the unique index below.
            migrationBuilder.Sql(
                """
                UPDATE [settings].[StoreSettings]
                   SET [TenantId] = '00000000-0000-0000-0000-000000000001'
                 WHERE [TenantId] = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StoreSettings_TenantId",
                schema: "settings",
                table: "StoreSettings",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoreSettings_TenantId",
                schema: "settings",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "settings",
                table: "StoreSettings");
        }
    }
}
