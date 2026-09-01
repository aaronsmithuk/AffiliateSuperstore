using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutonomousCatalogueShadowMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutonomousCatalogueDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EditorialVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EditorialVersionNumber = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReadinessScore = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    ReasonCodesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    PolicySnapshotJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvaluatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousCatalogueDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutonomousCatalogueDecisions_AutomationWorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "AutomationWorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AutonomousCatalogueDecisions_EditorialVersions_EditorialVersionId",
                        column: x => x.EditorialVersionId,
                        principalTable: "EditorialVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AutonomousCatalogueDecisions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "AliExpressProductId");
                    table.ForeignKey(
                        name: "FK_AutonomousCatalogueDecisions_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AutonomousCataloguePolicies",
                columns: table => new
                {
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReviewEveryHours = table.Column<int>(type: "int", nullable: false),
                    MaximumCandidatesPerRun = table.Column<int>(type: "int", nullable: false),
                    MaximumAutoPublishesPerDay = table.Column<int>(type: "int", nullable: false),
                    MinimumReadinessScore = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    DuplicateHoldConfidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    DailyAiBudgetUsd = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousCataloguePolicies", x => x.ShopId);
                    table.ForeignKey(
                        name: "FK_AutonomousCataloguePolicies_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousCatalogueDecisions_EditorialVersionId",
                table: "AutonomousCatalogueDecisions",
                column: "EditorialVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousCatalogueDecisions_ProductId",
                table: "AutonomousCatalogueDecisions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousCatalogueDecisions_ShopId_Action_EvaluatedUtc",
                table: "AutonomousCatalogueDecisions",
                columns: new[] { "ShopId", "Action", "EvaluatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousCatalogueDecisions_ShopId_EvaluatedUtc",
                table: "AutonomousCatalogueDecisions",
                columns: new[] { "ShopId", "EvaluatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousCatalogueDecisions_ShopId_ProductId_EditorialVersionNumber_EvaluatedUtc",
                table: "AutonomousCatalogueDecisions",
                columns: new[] { "ShopId", "ProductId", "EditorialVersionNumber", "EvaluatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousCatalogueDecisions_WorkItemId",
                table: "AutonomousCatalogueDecisions",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousCataloguePolicies_Mode_UpdatedUtc",
                table: "AutonomousCataloguePolicies",
                columns: new[] { "Mode", "UpdatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutonomousCatalogueDecisions");

            migrationBuilder.DropTable(
                name: "AutonomousCataloguePolicies");
        }
    }
}
