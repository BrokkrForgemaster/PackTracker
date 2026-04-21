using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestClaimsAndPinning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumberOfHelpersNeeded",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "NumberOfHelpersNeeded",
                table: "MaterialProcurementRequests");

            migrationBuilder.DropColumn(
                name: "NumberOfHelpersNeeded",
                table: "AssistanceRequests");

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "request_tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxClaims",
                table: "request_tickets",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "MaterialProcurementRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxClaims",
                table: "MaterialProcurementRequests",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "CraftingRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxClaims",
                table: "CraftingRequests",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "MaxClaims",
                table: "AssistanceRequests",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "RequestClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestClaims_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestClaims_ProfileId",
                table: "RequestClaims",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestClaims_RequestId_RequestType_ProfileId",
                table: "RequestClaims",
                columns: new[] { "RequestId", "RequestType", "ProfileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestClaims");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "MaxClaims",
                table: "request_tickets");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "MaterialProcurementRequests");

            migrationBuilder.DropColumn(
                name: "MaxClaims",
                table: "MaterialProcurementRequests");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "CraftingRequests");

            migrationBuilder.DropColumn(
                name: "MaxClaims",
                table: "CraftingRequests");

            migrationBuilder.DropColumn(
                name: "MaxClaims",
                table: "AssistanceRequests");

            migrationBuilder.AddColumn<int>(
                name: "NumberOfHelpersNeeded",
                table: "request_tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfHelpersNeeded",
                table: "MaterialProcurementRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfHelpersNeeded",
                table: "AssistanceRequests",
                type: "integer",
                nullable: true);
        }
    }
}
