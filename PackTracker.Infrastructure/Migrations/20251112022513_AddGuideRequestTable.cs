using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuideRequestTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssetsShips",
                table: "request_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Availability",
                table: "request_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CurrentBaseline",
                table: "request_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GameBuild",
                table: "request_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GroupPreference",
                table: "request_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "HasMic",
                table: "request_tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlatformSpecs",
                table: "request_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlayerHandle",
                table: "request_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecordingPermission",
                table: "request_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SkillObjective",
                table: "request_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SuccessCriteria",
                table: "request_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "request_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Urgency",
                table: "request_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Efficiency",
                table: "RegolithRefineryJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Eta",
                table: "RegolithRefineryJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Progress",
                table: "RegolithRefineryJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Yield",
                table: "RegolithRefineryJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "GuideRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Requester = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedToUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AssignedToUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuideRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuideRequests_ThreadId",
                table: "GuideRequests",
                column: "ThreadId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuideRequests");

            migrationBuilder.DropColumn(
                name: "AssetsShips",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "Availability",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "CurrentBaseline",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "GameBuild",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "GroupPreference",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "HasMic",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "PlatformSpecs",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "PlayerHandle",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "RecordingPermission",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "SkillObjective",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "SuccessCriteria",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "Urgency",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "Efficiency",
                table: "RegolithRefineryJobs");

            migrationBuilder.DropColumn(
                name: "Eta",
                table: "RegolithRefineryJobs");

            migrationBuilder.DropColumn(
                name: "Progress",
                table: "RegolithRefineryJobs");

            migrationBuilder.DropColumn(
                name: "Yield",
                table: "RegolithRefineryJobs");
        }
    }
}
