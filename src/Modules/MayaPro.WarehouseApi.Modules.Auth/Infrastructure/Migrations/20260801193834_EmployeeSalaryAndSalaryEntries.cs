using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure.Migrations
{
    /// <summary>
    /// BE#28 — the employee salary model, added to the <c>identity</c> schema without data loss:
    /// <list type="number">
    /// <item><c>Users.MonthlySalary</c>, the agreed monthly figure. Added with a <c>0</c> default so every
    /// existing employee reads as "no salary agreed yet" rather than NULL — the summary maths then never has
    /// to special-case a missing salary;</item>
    /// <item><c>SalaryEntries</c>, one row per payment or deduction on an employee's salary account. It
    /// carries two independent time fields on purpose: <c>Date</c> (when the cash moved — day-end and the
    /// dashboard filter on it, hence its own index) and <c>Month</c> (<c>yyyy-MM</c>, which month the line
    /// settles — the salary summary filters on <c>(UserId, Month)</c>, hence the composite index).</item>
    /// </list>
    /// <c>UserId</c> is a plain column, not a foreign key: employees are never deleted, and a salary history
    /// must survive independently of the employee row (same no-FK stance as <c>Expense.ProductId</c>).
    /// </summary>
    public partial class EmployeeSalaryAndSalaryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MonthlySalary",
                schema: "identity",
                table: "Users",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "SalaryEntries",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Month = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryEntries_Date",
                schema: "identity",
                table: "SalaryEntries",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryEntries_UserId_Month",
                schema: "identity",
                table: "SalaryEntries",
                columns: new[] { "UserId", "Month" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalaryEntries",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "MonthlySalary",
                schema: "identity",
                table: "Users");
        }
    }
}
