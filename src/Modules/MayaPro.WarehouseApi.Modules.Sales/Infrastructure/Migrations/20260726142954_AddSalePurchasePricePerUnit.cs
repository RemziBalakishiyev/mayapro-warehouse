using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalePurchasePricePerUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePricePerUnit",
                schema: "sales",
                table: "Sales",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Backfill existing rows so the new column reflects what was already knowable at sale time:
            //  - Normal (catalogued) sales predate any purchase-price snapshot, so they are left NULL —
            //    the frontend shows "—" for them (nothing is invented).
            //  - Free-form (manual) sales already stored a computed CostPerUnit and, optionally, free-form
            //    ExpenseItems documenting how it was derived; the purchase price is recovered by removing
            //    the per-unit expense share from CostPerUnit. With no expense lines there is nothing to
            //    remove, so PurchasePricePerUnit equals CostPerUnit as-is.
            //  - CostPerUnit NULL (cost genuinely unknown) or Quantity 0 (nothing to divide by) are handled
            //    explicitly so the backfill never throws or divides by zero.
            migrationBuilder.Sql(
                """
                UPDATE s
                SET s.PurchasePricePerUnit = CASE
                    WHEN s.CostPerUnit IS NULL THEN NULL
                    WHEN s.Quantity = 0 THEN s.CostPerUnit
                    ELSE s.CostPerUnit - (ISNULL(expenses.Total, 0) / s.Quantity)
                END
                FROM sales.Sales AS s
                OUTER APPLY (
                    SELECT SUM(CAST(JSON_VALUE(item.value, '$.amount') AS decimal(18,2))) AS Total
                    FROM OPENJSON(s.ExpenseItems) AS item
                ) AS expenses
                WHERE s.IsManual = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PurchasePricePerUnit",
                schema: "sales",
                table: "Sales");
        }
    }
}
