using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogueReviewAndJobMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShopProducts_ShopId_IsActive_IsFeatured_DisplayOrder",
                table: "ShopProducts");

            migrationBuilder.AddColumn<string>(
                name: "ReviewStatus",
                table: "ShopProducts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<int>(
                name: "LinksCreatedOrRefreshed",
                table: "IngestionJobs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE [IngestionJobs] SET [LinksCreatedOrRefreshed] = [ItemsWritten] " +
                "WHERE [Type] = N'CatalogueDiscovery' AND [ItemsWritten] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ShopProducts_ShopId_IsActive_ReviewStatus_IsFeatured_DisplayOrder",
                table: "ShopProducts",
                columns: new[] { "ShopId", "IsActive", "ReviewStatus", "IsFeatured", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShopProducts_ShopId_IsActive_ReviewStatus_IsFeatured_DisplayOrder",
                table: "ShopProducts");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "ShopProducts");

            migrationBuilder.DropColumn(
                name: "LinksCreatedOrRefreshed",
                table: "IngestionJobs");

            migrationBuilder.CreateIndex(
                name: "IX_ShopProducts_ShopId_IsActive_IsFeatured_DisplayOrder",
                table: "ShopProducts",
                columns: new[] { "ShopId", "IsActive", "IsFeatured", "DisplayOrder" });
        }
    }
}
