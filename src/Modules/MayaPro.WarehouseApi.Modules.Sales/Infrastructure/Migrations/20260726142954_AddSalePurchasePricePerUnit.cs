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
            // A migration must never abort a deployment over one odd row, so the JSON is read defensively:
            // ISJSON keeps a NULL/blank/corrupt ExpenseItems from breaking OPENJSON (treated as no expense
            // lines) and TRY_CAST turns a non-numeric amount into NULL, which SUM skips. The
            // "PurchasePricePerUnit IS NULL" filter makes the statement re-runnable: replaying it (e.g. by
            // hand on a partially migrated database) never overwrites a value written since.
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
                    SELECT SUM(TRY_CAST(JSON_VALUE(item.value, '$.amount') AS decimal(18,2))) AS Total
                    FROM OPENJSON(CASE WHEN ISJSON(s.ExpenseItems) = 1 THEN s.ExpenseItems ELSE N'[]' END) AS item
                ) AS expenses
                WHERE s.IsManual = 1
                  AND s.PurchasePricePerUnit IS NULL;
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
