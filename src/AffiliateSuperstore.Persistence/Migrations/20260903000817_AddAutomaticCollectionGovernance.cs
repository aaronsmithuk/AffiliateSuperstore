using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticCollectionGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionPublicationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IndexableProducts = table.Column<int>(type: "int", nullable: false),
                    RequiredProducts = table.Column<int>(type: "int", nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionPublicationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionPublicationEvents_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionPublicationEvents_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPublicationEvents_CollectionId_OccurredUtc",
                table: "CollectionPublicationEvents",
                columns: new[] { "CollectionId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPublicationEvents_ShopId_OccurredUtc",
                table: "CollectionPublicationEvents",
                columns: new[] { "ShopId", "OccurredUtc" });

            migrationBuilder.Sql(
                """
                UPDATE [AutonomousCataloguePolicies]
                SET [MaximumCandidatesPerRun] = 6,
                    [UpdatedUtc] = SYSUTCDATETIME(),
                    [UpdatedBy] = N'owner-approved migration: six candidates per hourly run'
                WHERE [MaximumCandidatesPerRun] = 5;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [AutonomousCataloguePolicies]
                SET [MaximumCandidatesPerRun] = 5,
                    [UpdatedUtc] = SYSUTCDATETIME(),
                    [UpdatedBy] = N'rollback: restore five candidates per run'
                WHERE [MaximumCandidatesPerRun] = 6
                  AND [UpdatedBy] = N'owner-approved migration: six candidates per hourly run';
                """);

            migrationBuilder.DropTable(
                name: "CollectionPublicationEvents");
        }
    }
}
