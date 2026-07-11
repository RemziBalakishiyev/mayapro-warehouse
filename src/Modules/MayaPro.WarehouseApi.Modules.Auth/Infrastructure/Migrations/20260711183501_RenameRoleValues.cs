using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameRoleValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Role is stored by enum name (HasConversion<string>). The enum members were renamed
            // Sahibkar/Menecer/Satici → Owner/Manager/Seller, so rewrite pre-existing (seeded) rows to the
            // new names. (Wire values "sahib"/"menecer"/"satici" are unaffected.)
            migrationBuilder.Sql("UPDATE [identity].[Users] SET Role = 'Owner' WHERE Role = 'Sahibkar';");
            migrationBuilder.Sql("UPDATE [identity].[Users] SET Role = 'Manager' WHERE Role = 'Menecer';");
            migrationBuilder.Sql("UPDATE [identity].[Users] SET Role = 'Seller' WHERE Role = 'Satici';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [identity].[Users] SET Role = 'Sahibkar' WHERE Role = 'Owner';");
            migrationBuilder.Sql("UPDATE [identity].[Users] SET Role = 'Menecer' WHERE Role = 'Manager';");
            migrationBuilder.Sql("UPDATE [identity].[Users] SET Role = 'Satici' WHERE Role = 'Seller';");
        }
    }
}
