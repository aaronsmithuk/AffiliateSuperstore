using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductIdentityGoldLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductIdentityGoldLabels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Slice = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reviewer = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsAdjudication = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductIdentityGoldLabels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductIdentityGoldLabels_ProductMatchCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "ProductMatchCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductIdentityGoldLabels_CandidateId_CreatedUtc",
                table: "ProductIdentityGoldLabels",
                columns: new[] { "CandidateId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductIdentityGoldLabels_Reviewer_CreatedUtc",
                table: "ProductIdentityGoldLabels",
                columns: new[] { "Reviewer", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductIdentityGoldLabels_Slice_CreatedUtc",
                table: "ProductIdentityGoldLabels",
                columns: new[] { "Slice", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductIdentityGoldLabels");
        }
    }
}
