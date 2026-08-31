using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionedEditorialContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentEditorialVersionNumber",
                table: "ShopProducts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EditorialValidatedUtc",
                table: "ShopProducts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditorialValidationFlags",
                table: "ShopProducts",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditorialValidationState",
                table: "ShopProducts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotEvaluated");

            migrationBuilder.CreateTable(
                name: "EditorialVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    EditorialTitle = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EditorialDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ChangeKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RolledBackFromVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ValidationState = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ValidationFindingsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ValidatorVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EditorialVersions_EditorialVersions_RolledBackFromVersionId",
                        column: x => x.RolledBackFromVersionId,
                        principalTable: "EditorialVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EditorialVersions_ShopProducts_ShopId_ProductId",
                        columns: x => new { x.ShopId, x.ProductId },
                        principalTable: "ShopProducts",
                        principalColumns: new[] { "ShopId", "ProductId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialVersions_RolledBackFromVersionId",
                table: "EditorialVersions",
                column: "RolledBackFromVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_EditorialVersions_ShopId_CreatedUtc",
                table: "EditorialVersions",
                columns: new[] { "ShopId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialVersions_ShopId_ProductId_VersionNumber",
                table: "EditorialVersions",
                columns: new[] { "ShopId", "ProductId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EditorialVersions");

            migrationBuilder.DropColumn(
                name: "CurrentEditorialVersionNumber",
                table: "ShopProducts");

            migrationBuilder.DropColumn(
                name: "EditorialValidatedUtc",
                table: "ShopProducts");

            migrationBuilder.DropColumn(
                name: "EditorialValidationFlags",
                table: "ShopProducts");

            migrationBuilder.DropColumn(
                name: "EditorialValidationState",
                table: "ShopProducts");
        }
    }
}
