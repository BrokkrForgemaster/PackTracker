using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreCraftingRequestColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ItemName",
                table: "CraftingRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialSupplyMode",
                table: "CraftingRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ItemName",
                table: "CraftingRequests");

            migrationBuilder.DropColumn(
                name: "MaterialSupplyMode",
                table: "CraftingRequests");
        }
    }
}
