using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMedalNominations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedalNominations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedalDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NomineeProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    NomineeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NominatorProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    NominatorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Citation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedalNominations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedalNominations_MedalDefinitions_MedalDefinitionId",
                        column: x => x.MedalDefinitionId,
                        principalTable: "MedalDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedalNominations_Profiles_NomineeProfileId",
                        column: x => x.NomineeProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedalNominations_MedalDefinitionId",
                table: "MedalNominations",
                column: "MedalDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MedalNominations_NomineeProfileId",
                table: "MedalNominations",
                column: "NomineeProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MedalNominations_Status",
                table: "MedalNominations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MedalNominations_SubmittedAt",
                table: "MedalNominations",
                column: "SubmittedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedalNominations");
        }
    }
}
