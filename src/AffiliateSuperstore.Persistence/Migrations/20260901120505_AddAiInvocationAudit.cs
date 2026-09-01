using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiInvocationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiInvocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InputHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CacheKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProviderResponseId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResponseHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    ReservedCostUsd = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    LatencyMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    EditorialValidationState = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ValidationFindingsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInvocations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiInvocations_CacheKey_Status_CompletedUtc",
                table: "AiInvocations",
                columns: new[] { "CacheKey", "Status", "CompletedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInvocations_ProductId_RequestedUtc",
                table: "AiInvocations",
                columns: new[] { "ProductId", "RequestedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInvocations_Purpose_RequestedUtc",
                table: "AiInvocations",
                columns: new[] { "Purpose", "RequestedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInvocations_RequestedUtc_Status",
                table: "AiInvocations",
                columns: new[] { "RequestedUtc", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiInvocations");
        }
    }
}
