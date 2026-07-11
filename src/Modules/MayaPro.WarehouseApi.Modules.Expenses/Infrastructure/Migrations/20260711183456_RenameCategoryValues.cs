using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Expenses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameCategoryValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Category is stored by enum name (HasConversion<string>). The enum members were renamed, so
            // rewrite pre-existing rows to the new names. (Wire values "Yol"/"Fəhlə"/... are unaffected.)
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET Category = 'Transport' WHERE Category = 'Yol';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET Category = 'Labor' WHERE Category = 'Fehle';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET Category = 'Storage' WHERE Category = 'AnbarYer';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET Category = 'Packaging' WHERE Category = 'PaketQutu';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET Category = 'Store' WHERE Category = 'Magaza';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET Category = 'Other' WHERE Category = 'Diger';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET Category = 'Yol' WHERE Category = 'Transport';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET Category = 'Fehle' WHERE Category = 'Labor';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET Category = 'AnbarYer' WHERE Category = 'Storage';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET Category = 'PaketQutu' WHERE Category = 'Packaging';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET Category = 'Magaza' WHERE Category = 'Store';");
            migrationBuilder.Sql("UPDATE [expenses].[Expenses] SET Category = 'Diger' WHERE Category = 'Other';");
        }
    }
}
