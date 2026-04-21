using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationToRequestComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "RequestComments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "RequestComments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceBuy",
                table: "Commodities",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceSell",
                table: "Commodities",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BlueprintId1",
                table: "BlueprintRecipes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DistributedLocks",
                columns: table => new
                {
                    LockKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LockedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributedLocks", x => x.LockKey);
                });

            migrationBuilder.CreateTable(
                name: "LoginStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientState = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AccessToken = table.Column<string>(type: "text", nullable: false),
                    RefreshToken = table.Column<string>(type: "text", nullable: false),
                    ExpiresIn = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncMetadatas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    LastErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ItemsProcessed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncMetadatas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintRecipes_BlueprintId1",
                table: "BlueprintRecipes",
                column: "BlueprintId1",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginStates_ClientState",
                table: "LoginStates",
                column: "ClientState",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncMetadatas_TaskName",
                table: "SyncMetadatas",
                column: "TaskName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BlueprintRecipes_Blueprints_BlueprintId1",
                table: "BlueprintRecipes",
                column: "BlueprintId1",
                principalTable: "Blueprints",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlueprintRecipes_Blueprints_BlueprintId1",
                table: "BlueprintRecipes");

            migrationBuilder.DropTable(
                name: "DistributedLocks");

            migrationBuilder.DropTable(
                name: "LoginStates");

            migrationBuilder.DropTable(
                name: "SyncMetadatas");

            migrationBuilder.DropIndex(
                name: "IX_BlueprintRecipes_BlueprintId1",
                table: "BlueprintRecipes");

            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "RequestComments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "RequestComments");

            migrationBuilder.DropColumn(
                name: "PriceBuy",
                table: "Commodities");

            migrationBuilder.DropColumn(
                name: "PriceSell",
                table: "Commodities");

            migrationBuilder.DropColumn(
                name: "BlueprintId1",
                table: "BlueprintRecipes");
        }
    }
}
