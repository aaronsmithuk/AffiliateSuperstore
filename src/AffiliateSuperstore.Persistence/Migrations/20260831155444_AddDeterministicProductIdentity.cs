using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeterministicProductIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CanonicalProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanonicalProducts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductIdentityProfiles",
                columns: table => new
                {
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NormalizedTitle = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    NormalizedGtin = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    NormalizedModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PackCount = table.Column<int>(type: "int", nullable: true),
                    SizeCentimetres = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: true),
                    Colour = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Material = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TokensJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    InputHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NormalizerVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductIdentityProfiles", x => x.ProductId);
                    table.ForeignKey(
                        name: "FK_ProductIdentityProfiles_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "AliExpressProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductMatchCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeftProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RightProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SuggestedRelationship = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(6,5)", precision: 6, scale: 5, nullable: false),
                    BlockingReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ConflictJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MatcherVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GeneratedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMatchCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductMatchCandidates_Products_LeftProductId",
                        column: x => x.LeftProductId,
                        principalTable: "Products",
                        principalColumn: "AliExpressProductId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductMatchCandidates_Products_RightProductId",
                        column: x => x.RightProductId,
                        principalTable: "Products",
                        principalColumn: "AliExpressProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CanonicalProductMembers",
                columns: table => new
                {
                    CanonicalProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EvidenceCandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanonicalProductMembers", x => new { x.CanonicalProductId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_CanonicalProductMembers_CanonicalProducts_CanonicalProductId",
                        column: x => x.CanonicalProductId,
                        principalTable: "CanonicalProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CanonicalProductMembers_ProductMatchCandidates_EvidenceCandidateId",
                        column: x => x.EvidenceCandidateId,
                        principalTable: "ProductMatchCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CanonicalProductMembers_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "AliExpressProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalProductMembers_EvidenceCandidateId",
                table: "CanonicalProductMembers",
                column: "EvidenceCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalProductMembers_ProductId",
                table: "CanonicalProductMembers",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductIdentityProfiles_NormalizedGtin",
                table: "ProductIdentityProfiles",
                column: "NormalizedGtin");

            migrationBuilder.CreateIndex(
                name: "IX_ProductIdentityProfiles_PackCount_SizeCentimetres",
                table: "ProductIdentityProfiles",
                columns: new[] { "PackCount", "SizeCentimetres" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductMatchCandidates_LeftProductId_RightProductId_MatcherVersion",
                table: "ProductMatchCandidates",
                columns: new[] { "LeftProductId", "RightProductId", "MatcherVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductMatchCandidates_ReviewStatus_IsCurrent_Confidence_GeneratedUtc",
                table: "ProductMatchCandidates",
                columns: new[] { "ReviewStatus", "IsCurrent", "Confidence", "GeneratedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductMatchCandidates_RightProductId",
                table: "ProductMatchCandidates",
                column: "RightProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanonicalProductMembers");

            migrationBuilder.DropTable(
                name: "ProductIdentityProfiles");

            migrationBuilder.DropTable(
                name: "CanonicalProducts");

            migrationBuilder.DropTable(
                name: "ProductMatchCandidates");
        }
    }
}
