using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWikiSyncFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Materials",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WikiUuid",
                table: "Materials",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WikiLastSyncedAt",
                table: "Blueprints",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WikiUuid",
                table: "Blueprints",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_WikiUuid",
                table: "Materials",
                column: "WikiUuid");

            migrationBuilder.CreateIndex(
                name: "IX_Blueprints_WikiUuid",
                table: "Blueprints",
                column: "WikiUuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Materials_WikiUuid",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Blueprints_WikiUuid",
                table: "Blueprints");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "WikiUuid",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "WikiLastSyncedAt",
                table: "Blueprints");

            migrationBuilder.DropColumn(
                name: "WikiUuid",
                table: "Blueprints");
        }
    }
}
