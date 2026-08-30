using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateS2sInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AffiliateS2sEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SubOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClickId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TrackingId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrderAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CommissionRate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    EstimatedCommission = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ShipToCountry = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsAffiliateProduct = table.Column<bool>(type: "bit", nullable: true),
                    IsHotProduct = table.Column<bool>(type: "bit", nullable: true),
                    EffectPayUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReceivedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateS2sEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateS2sEvents_ClickId",
                table: "AffiliateS2sEvents",
                column: "ClickId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateS2sEvents_EventKey",
                table: "AffiliateS2sEvents",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateS2sEvents_SubOrderId_ReceivedUtc",
                table: "AffiliateS2sEvents",
                columns: new[] { "SubOrderId", "ReceivedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffiliateS2sEvents");
        }
    }
}
