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

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintRecipes_BlueprintId1",
                table: "BlueprintRecipes",
                column: "BlueprintId1",
                unique: true,
                filter: "\"BlueprintId1\" IS NOT NULL");

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
