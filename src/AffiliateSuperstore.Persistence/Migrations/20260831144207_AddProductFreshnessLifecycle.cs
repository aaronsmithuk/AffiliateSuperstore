using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductFreshnessLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "ProductSnapshots",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "ProductSnapshots",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservationHash",
                table: "ProductSnapshots",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParserVersion",
                table: "ProductSnapshots",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "1.0");

            migrationBuilder.AddColumn<string>(
                name: "SourceEndpoint",
                table: "ProductSnapshots",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvailabilityChangedUtc",
                table: "Products",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvailabilityReason",
                table: "Products",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvailabilityState",
                table: "Products",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Available");

            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveUnavailableChecks",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CurrentContentHash",
                table: "Products",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentObservationHash",
                table: "Products",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstUnavailableEvidenceUtc",
                table: "Products",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastCheckedUtc",
                table: "Products",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSuccessfulCheckUtc",
                table: "Products",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastUnavailableEvidenceUtc",
                table: "Products",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [Products]
                SET [LastCheckedUtc] = [LastRefreshedUtc],
                    [LastSuccessfulCheckUtc] = [LastSeenUtc]
                WHERE [LastCheckedUtc] IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "ProductChangeEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EvidenceSource = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PreviousValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CurrentValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ObservationHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductChangeEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductChangeEvents_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "AliExpressProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSnapshots_ProductId_ContentHash",
                table: "ProductSnapshots",
                columns: new[] { "ProductId", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_AvailabilityState_LastCheckedUtc",
                table: "Products",
                columns: new[] { "AvailabilityState", "LastCheckedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductChangeEvents_Kind_OccurredUtc",
                table: "ProductChangeEvents",
                columns: new[] { "Kind", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductChangeEvents_ProductId_OccurredUtc",
                table: "ProductChangeEvents",
                columns: new[] { "ProductId", "OccurredUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductChangeEvents");

            migrationBuilder.DropIndex(
                name: "IX_ProductSnapshots_ProductId_ContentHash",
                table: "ProductSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_Products_AvailabilityState_LastCheckedUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "ProductSnapshots");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "ProductSnapshots");

            migrationBuilder.DropColumn(
                name: "ObservationHash",
                table: "ProductSnapshots");

            migrationBuilder.DropColumn(
                name: "ParserVersion",
                table: "ProductSnapshots");

            migrationBuilder.DropColumn(
                name: "SourceEndpoint",
                table: "ProductSnapshots");

            migrationBuilder.DropColumn(
                name: "AvailabilityChangedUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AvailabilityReason",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AvailabilityState",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ConsecutiveUnavailableChecks",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CurrentContentHash",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CurrentObservationHash",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FirstUnavailableEvidenceUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LastCheckedUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulCheckUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LastUnavailableEvidenceUtc",
                table: "Products");
        }
    }
}
