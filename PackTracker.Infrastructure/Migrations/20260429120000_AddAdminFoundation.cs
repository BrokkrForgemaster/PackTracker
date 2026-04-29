using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PackTracker.Infrastructure.Persistence;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260429120000_AddAdminFoundation")]
    public partial class AddAdminFoundation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    Severity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminAuditLogs_Profiles_ActorProfileId",
                        column: x => x.ActorProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdminRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscordIntegrationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MedalAnnouncementsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RecruitingPostsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OperationsChannelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MedalAnnouncementsChannelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RecruitingPostsChannelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdatedByProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordIntegrationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscordIntegrationSettings_Profiles_UpdatedByProfileId",
                        column: x => x.UpdatedByProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdminPermissionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminPermissionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminPermissionAssignments_AdminRoles_AdminRoleId",
                        column: x => x.AdminRoleId,
                        principalTable: "AdminRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberRoleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedByProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberRoleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberRoleAssignments_AdminRoles_AdminRoleId",
                        column: x => x.AdminRoleId,
                        principalTable: "AdminRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberRoleAssignments_Profiles_AssignedByProfileId",
                        column: x => x.AssignedByProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberRoleAssignments_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_ActorProfileId_OccurredAt",
                table: "AdminAuditLogs",
                columns: new[] { "ActorProfileId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_OccurredAt",
                table: "AdminAuditLogs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_TargetType_TargetId",
                table: "AdminAuditLogs",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminPermissionAssignments_AdminRoleId_PermissionKey",
                table: "AdminPermissionAssignments",
                columns: new[] { "AdminRoleId", "PermissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminRoles_Name",
                table: "AdminRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscordIntegrationSettings_UpdatedByProfileId",
                table: "DiscordIntegrationSettings",
                column: "UpdatedByProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberRoleAssignments_AdminRoleId",
                table: "MemberRoleAssignments",
                column: "AdminRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberRoleAssignments_AssignedByProfileId",
                table: "MemberRoleAssignments",
                column: "AssignedByProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberRoleAssignments_ProfileId",
                table: "MemberRoleAssignments",
                column: "ProfileId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs");

            migrationBuilder.DropTable(
                name: "AdminPermissionAssignments");

            migrationBuilder.DropTable(
                name: "DiscordIntegrationSettings");

            migrationBuilder.DropTable(
                name: "MemberRoleAssignments");

            migrationBuilder.DropTable(
                name: "AdminRoles");
        }
    }
}
