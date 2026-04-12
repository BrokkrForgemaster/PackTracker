using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlphaReadinessPersistenceStandardization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequesterTimeZoneDisplayName",
                table: "CraftingRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequesterUtcOffsetMinutes",
                table: "CraftingRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssistanceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaterialName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    QuantityNeeded = table.Column<int>(type: "integer", nullable: true),
                    MeetingLocation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RewardOffered = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NumberOfHelpersNeeded = table.Column<int>(type: "integer", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistanceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistanceRequests_Profiles_AssignedToProfileId",
                        column: x => x.AssignedToProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssistanceRequests_Profiles_CreatedByProfileId",
                        column: x => x.CreatedByProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistanceRequests_AssignedToProfileId",
                table: "AssistanceRequests",
                column: "AssignedToProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistanceRequests_CreatedByProfileId",
                table: "AssistanceRequests",
                column: "CreatedByProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistanceRequests");

            migrationBuilder.DropColumn(
                name: "RequesterTimeZoneDisplayName",
                table: "CraftingRequests");

            migrationBuilder.DropColumn(
                name: "RequesterUtcOffsetMinutes",
                table: "CraftingRequests");
        }
    }
}
