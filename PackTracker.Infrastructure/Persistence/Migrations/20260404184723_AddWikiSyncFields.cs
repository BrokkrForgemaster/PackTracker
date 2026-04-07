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
            // Use raw SQL for idempotent column/index addition to avoid failure if they already exist from previous non-migration runs.
            migrationBuilder.Sql(@"ALTER TABLE ""Materials"" ADD COLUMN IF NOT EXISTS ""Category"" character varying(100)");
            migrationBuilder.Sql(@"ALTER TABLE ""Materials"" ADD COLUMN IF NOT EXISTS ""WikiUuid"" character varying(200)");
            migrationBuilder.Sql(@"ALTER TABLE ""Blueprints"" ADD COLUMN IF NOT EXISTS ""WikiLastSyncedAt"" character varying(50)");
            migrationBuilder.Sql(@"ALTER TABLE ""Blueprints"" ADD COLUMN IF NOT EXISTS ""WikiUuid"" character varying(200)");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Materials_WikiUuid"" ON ""Materials"" (""WikiUuid"")");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Blueprints_WikiUuid"" ON ""Blueprints"" (""WikiUuid"")");
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
