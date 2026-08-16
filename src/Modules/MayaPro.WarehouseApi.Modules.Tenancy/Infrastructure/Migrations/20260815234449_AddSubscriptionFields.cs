using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure.Migrations
{
    /// <summary>
    /// BE#36 — the subscription. Two columns on <c>Tenants</c> plus the platform's payment ledger.
    /// <para>
    /// <b>No back-fill, on purpose.</b> <c>ExpiresAt</c> is added as <c>NULL</c> and every existing shop
    /// (including the default "İlk Mağaza") keeps that value. <c>NULL</c> means open-ended — never expired —
    /// so a running installation cannot lock itself out the moment it upgrades. A shop only ever gets an end
    /// date when the platform admin approves it or records a payment against it.
    /// </para>
    /// <para>
    /// <c>MonthlyFee</c> defaults to 0 ("not agreed yet") and is never null, so the admin totals stay simple.
    /// <c>SubscriptionPayments.TenantId</c> is a bare Guid, not an FK: modules never wire navigations across
    /// each other's rows, and the payment ledger is platform-level data that no shop can read.
    /// </para>
    /// </summary>
    public partial class AddSubscriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                schema: "tenancy",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyFee",
                schema: "tenancy",
                table: "Tenants",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "SubscriptionPayments",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodMonths = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPayments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ExpiresAt",
                schema: "tenancy",
                table: "Tenants",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPayments_PaidAt",
                schema: "tenancy",
                table: "SubscriptionPayments",
                column: "PaidAt");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPayments_TenantId_PaidAt",
                schema: "tenancy",
                table: "SubscriptionPayments",
                columns: new[] { "TenantId", "PaidAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionPayments",
                schema: "tenancy");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_ExpiresAt",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MonthlyFee",
                schema: "tenancy",
                table: "Tenants");
        }
    }
}
