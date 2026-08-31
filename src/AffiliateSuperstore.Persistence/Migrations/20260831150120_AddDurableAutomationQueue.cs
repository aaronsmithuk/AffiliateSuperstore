using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableAutomationQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomationWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    QueuedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AvailableUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseOwner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LeaseExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaximumAttempts = table.Column<int>(type: "int", nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResultJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationWorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationWorkItems_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationWorkItems_IdempotencyKey",
                table: "AutomationWorkItems",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationWorkItems_LeaseExpiresUtc",
                table: "AutomationWorkItems",
                column: "LeaseExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationWorkItems_ShopId_Type_QueuedUtc",
                table: "AutomationWorkItems",
                columns: new[] { "ShopId", "Type", "QueuedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationWorkItems_Status_AvailableUtc_Priority",
                table: "AutomationWorkItems",
                columns: new[] { "Status", "AvailableUtc", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationWorkItems");
        }
    }
}
