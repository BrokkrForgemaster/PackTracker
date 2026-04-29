using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Queries.GetAdminSettings;

public sealed record GetAdminSettingsQuery() : IRequest<AdminSettingsDto>;

public sealed class GetAdminSettingsQueryHandler : IRequestHandler<GetAdminSettingsQuery, AdminSettingsDto>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;

    public GetAdminSettingsQueryHandler(IAdminDbContext db, IAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<AdminSettingsDto> Handle(GetAdminSettingsQuery request, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.SettingsView, cancellationToken);

        var settings = await _db.DiscordIntegrationSettings
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstAsync(cancellationToken);

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
