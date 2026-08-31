using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AffiliateSuperstore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationWorkCheckpoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Checkpoint",
                table: "AutomationWorkItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Checkpoint",
                table: "AutomationWorkItems");
        }
    }
}
