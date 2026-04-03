using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceRequestTicketForMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaterialName",
                table: "request_tickets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingLocation",
                table: "request_tickets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfHelpersNeeded",
                table: "request_tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityNeeded",
                table: "request_tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RewardOffered",
                table: "request_tickets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaterialName",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "MeetingLocation",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "NumberOfHelpersNeeded",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "QuantityNeeded",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "RewardOffered",
                table: "request_tickets");
        }
    }
}
