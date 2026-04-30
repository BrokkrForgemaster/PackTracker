using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    public partial class AddProfileShowcaseFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShowcaseBio",
                table: "Profiles",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShowcaseEyebrow",
                table: "Profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShowcaseImageUrl",
                table: "Profiles",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShowcaseTagline",
                table: "Profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ShowcaseBio", table: "Profiles");
            migrationBuilder.DropColumn(name: "ShowcaseEyebrow", table: "Profiles");
            migrationBuilder.DropColumn(name: "ShowcaseImageUrl", table: "Profiles");
            migrationBuilder.DropColumn(name: "ShowcaseTagline", table: "Profiles");
        }
    }
}
