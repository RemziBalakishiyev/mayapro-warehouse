using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Settings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreAddressPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "settings",
                table: "StoreSettings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "settings",
                table: "StoreSettings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                schema: "settings",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "settings",
                table: "StoreSettings");
        }
    }
}
