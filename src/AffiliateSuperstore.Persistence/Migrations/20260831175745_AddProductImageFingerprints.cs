using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImageFingerprints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductImageFingerprints",
                columns: table => new
                {
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    SourceUrlHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ContentLength = table.Column<long>(type: "bigint", nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FingerprintedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FingerprinterVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImageFingerprints", x => x.ProductId);
                    table.ForeignKey(
                        name: "FK_ProductImageFingerprints_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "AliExpressProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImageFingerprints_ContentSha256",
                table: "ProductImageFingerprints",
                column: "ContentSha256");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImageFingerprints_Status_LastAttemptUtc",
                table: "ProductImageFingerprints",
                columns: new[] { "Status", "LastAttemptUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductImageFingerprints");
        }
    }
}
