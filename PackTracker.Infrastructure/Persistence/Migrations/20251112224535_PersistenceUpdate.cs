using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistenceUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRevoked",
                table: "RefreshTokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProfileId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ProfileId",
                table: "RefreshTokens",
                column: "ProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Profiles_ProfileId",
                table: "RefreshTokens",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Profiles_ProfileId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_ProfileId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "IsRevoked",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "RefreshTokens");
        }
    }
}
