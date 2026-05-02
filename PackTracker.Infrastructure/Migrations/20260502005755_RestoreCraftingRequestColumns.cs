using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreCraftingRequestColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "CraftingRequests"
                    ADD COLUMN IF NOT EXISTS "ItemName" text,
                    ADD COLUMN IF NOT EXISTS "MaterialSupplyMode" integer NOT NULL DEFAULT 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "CraftingRequests"
                    DROP COLUMN IF EXISTS "ItemName",
                    DROP COLUMN IF EXISTS "MaterialSupplyMode";
                """);
        }
    }
}
