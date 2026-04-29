using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Commands.UpdateAdminSettings;

public sealed record UpdateAdminSettingsCommand(UpdateAdminSettingsRequestDto Request) : IRequest<AdminSettingsDto>;

public sealed class UpdateAdminSettingsCommandHandler : IRequestHandler<UpdateAdminSettingsCommand, AdminSettingsDto>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditLogService _audit;
    private readonly IRbacService _rbac;

    public UpdateAdminSettingsCommandHandler(
        IAdminDbContext db,
        IAuthorizationService authorization,
        IAuditLogService audit,
        IRbacService rbac)
    {
        _db = db;
        _authorization = authorization;
        _audit = audit;
        _rbac = rbac;
    }

    public async Task<AdminSettingsDto> Handle(UpdateAdminSettingsCommand command, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.SettingsDiscordManage, cancellationToken);

        var context = await _rbac.GetCurrentAdminContextAsync(cancellationToken);
        var settings = await _db.DiscordIntegrationSettings
            .OrderByDescending(x => x.UpdatedAt)
            .FirstAsync(cancellationToken);

        var beforeJson = JsonSerializer.Serialize(settings);

        settings.OperationsEnabled = command.Request.OperationsEnabled;
        settings.MedalAnnouncementsEnabled = command.Request.MedalAnnouncementsEnabled;
        settings.RecruitingPostsEnabled = command.Request.RecruitingPostsEnabled;
        settings.OperationsChannelId = command.Request.OperationsChannelId;
        settings.MedalAnnouncementsChannelId = command.Request.MedalAnnouncementsChannelId;
        settings.RecruitingPostsChannelId = command.Request.RecruitingPostsChannelId;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByProfileId = context.ProfileId;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            new AdminAuditLogEntryDto(
                "AdminSettingsUpdated",
                "DiscordIntegrationSetting",
                settings.Id.ToString(),
                "Updated admin Discord integration settings.",
                "Info",
                beforeJson,
                JsonSerializer.Serialize(settings)),
            cancellationToken);

        return new AdminSettingsDto(
            settings.OperationsEnabled,
            settings.MedalAnnouncementsEnabled,
            settings.RecruitingPostsEnabled,
            settings.OperationsChannelId,
            settings.MedalAnnouncementsChannelId,
            settings.RecruitingPostsChannelId,
            settings.UpdatedAt);
    }
}
