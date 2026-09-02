using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionSuggestionDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiInvocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IntroductoryCopy = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SeoTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SeoDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DiscoveryQueriesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceProductIdsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InputHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionSuggestions_AiInvocations_AiInvocationId",
                        column: x => x.AiInvocationId,
                        principalTable: "AiInvocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CollectionSuggestions_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionSuggestions_AiInvocationId",
                table: "CollectionSuggestions",
                column: "AiInvocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionSuggestions_ShopId_Slug_Status",
                table: "CollectionSuggestions",
                columns: new[] { "ShopId", "Slug", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionSuggestions_ShopId_Status_CreatedUtc",
                table: "CollectionSuggestions",
                columns: new[] { "ShopId", "Status", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionSuggestions");
        }
    }
}
