using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddAssistancePinsAndLiveChatFlags : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsPinned",
            table: "AssistanceRequests",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsLiveChat",
            table: "RequestComments",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsPinned",
            table: "AssistanceRequests");

        migrationBuilder.DropColumn(
            name: "IsLiveChat",
            table: "RequestComments");
    }
}
