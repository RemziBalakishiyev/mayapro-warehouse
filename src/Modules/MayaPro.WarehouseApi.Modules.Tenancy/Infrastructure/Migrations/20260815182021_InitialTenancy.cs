using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.CreateTable(
                name: "Tenants",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status",
                schema: "tenancy",
                table: "Tenants",
                column: "Status");

            // BE#35 — the default shop. Every row that existed before multi-tenancy is back-filled to this
            // id by each module's own data migration, so the id must be a fixed constant (TenantDefaults)
            // rather than a generated value. Status = 1 (Active) so the existing installation keeps working
            // the moment the migration finishes. Guarded by NOT EXISTS: re-running the migration on a
            // database that already carries the row is a no-op, never a second "İlk Mağaza".
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM [tenancy].[Tenants] WHERE [Id] = '00000000-0000-0000-0000-000000000001')
                    INSERT INTO [tenancy].[Tenants] ([Id], [Name], [OwnerName], [Phone], [Status], [CreatedAt], [UpdatedAt])
                    VALUES ('00000000-0000-0000-0000-000000000001', N'İlk Mağaza', NULL, NULL, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "tenancy");
        }
    }
}
