using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateOrderIncentives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "IncentiveCommission",
                table: "AffiliateS2sEvents",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IncentiveCommissionRate",
                table: "AffiliateS2sEvents",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNewBuyer",
                table: "AffiliateS2sEvents",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NewBuyerBonus",
                table: "AffiliateS2sEvents",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderPlatform",
                table: "AffiliateS2sEvents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderType",
                table: "AffiliateS2sEvents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedIncentivePaidCommission",
                table: "AffiliateOrders",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IncentiveCommissionRate",
                table: "AffiliateOrders",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNewBuyer",
                table: "AffiliateOrders",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NewBuyerBonusCommission",
                table: "AffiliateOrders",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderPlatform",
                table: "AffiliateOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderType",
                table: "AffiliateOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncentiveCommission",
                table: "AffiliateS2sEvents");

            migrationBuilder.DropColumn(
                name: "IncentiveCommissionRate",
                table: "AffiliateS2sEvents");

            migrationBuilder.DropColumn(
                name: "IsNewBuyer",
                table: "AffiliateS2sEvents");

            migrationBuilder.DropColumn(
                name: "NewBuyerBonus",
                table: "AffiliateS2sEvents");

            migrationBuilder.DropColumn(
                name: "OrderPlatform",
                table: "AffiliateS2sEvents");

            migrationBuilder.DropColumn(
                name: "OrderType",
                table: "AffiliateS2sEvents");

            migrationBuilder.DropColumn(
                name: "EstimatedIncentivePaidCommission",
                table: "AffiliateOrders");

            migrationBuilder.DropColumn(
                name: "IncentiveCommissionRate",
                table: "AffiliateOrders");

            migrationBuilder.DropColumn(
                name: "IsNewBuyer",
                table: "AffiliateOrders");

            migrationBuilder.DropColumn(
                name: "NewBuyerBonusCommission",
                table: "AffiliateOrders");

            migrationBuilder.DropColumn(
                name: "OrderPlatform",
                table: "AffiliateOrders");

            migrationBuilder.DropColumn(
                name: "OrderType",
                table: "AffiliateOrders");
        }
    }
}
