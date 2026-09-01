using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifiedEditorialFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VerificationEvidence",
                table: "ShopProducts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedOptions",
                table: "ShopProducts",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedSize",
                table: "ShopProducts",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationEvidence",
                table: "EditorialVersions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedOptions",
                table: "EditorialVersions",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedSize",
                table: "EditorialVersions",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationEvidence",
                table: "ShopProducts");

            migrationBuilder.DropColumn(
                name: "VerifiedOptions",
                table: "ShopProducts");

            migrationBuilder.DropColumn(
                name: "VerifiedSize",
                table: "ShopProducts");

            migrationBuilder.DropColumn(
                name: "VerificationEvidence",
                table: "EditorialVersions");

            migrationBuilder.DropColumn(
                name: "VerifiedOptions",
                table: "EditorialVersions");

            migrationBuilder.DropColumn(
                name: "VerifiedSize",
                table: "EditorialVersions");
        }
    }
}
