using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenamePaymentTypeValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PaymentType is stored by enum name (HasConversion<string>). The enum members were renamed
            // Nagd/Kart/Nisye → Cash/Card/Credit, so any rows written before this migration carry the old
            // names — rewrite them so EF can read them back. (Wire values "Nağd"/... are unaffected.)
            migrationBuilder.Sql("UPDATE [sales].[Sales] SET PaymentType = 'Cash' WHERE PaymentType = 'Nagd';");
            migrationBuilder.Sql("UPDATE [sales].[Sales] SET PaymentType = 'Card' WHERE PaymentType = 'Kart';");
            migrationBuilder.Sql("UPDATE [sales].[Sales] SET PaymentType = 'Credit' WHERE PaymentType = 'Nisye';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [sales].[Sales] SET PaymentType = 'Nagd' WHERE PaymentType = 'Cash';");
            migrationBuilder.Sql("UPDATE [sales].[Sales] SET PaymentType = 'Kart' WHERE PaymentType = 'Card';");
            migrationBuilder.Sql("UPDATE [sales].[Sales] SET PaymentType = 'Nisye' WHERE PaymentType = 'Credit';");
        }
    }
}
