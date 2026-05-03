using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAwardTypeToMedals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwardType",
                table: "MedalDefinitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Medal");

            migrationBuilder.AddColumn<string>(
                name: "AwardType",
                table: "MedalAwards",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Medal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwardType",
                table: "MedalDefinitions");

            migrationBuilder.DropColumn(
                name: "AwardType",
                table: "MedalAwards");
        }
    }
}
