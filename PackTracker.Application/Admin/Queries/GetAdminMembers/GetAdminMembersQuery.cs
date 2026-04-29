using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Queries.GetAdminMembers;

public sealed record GetAdminMembersQuery() : IRequest<IReadOnlyList<AdminMemberListItemDto>>;

public sealed class GetAdminMembersQueryHandler : IRequestHandler<GetAdminMembersQuery, IReadOnlyList<AdminMemberListItemDto>>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;

    public GetAdminMembersQueryHandler(IAdminDbContext db, IAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<AdminMemberListItemDto>> Handle(GetAdminMembersQuery request, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.MembersView, cancellationToken);

        var profiles = await _db.Profiles
            .AsNoTracking()
            .OrderBy(x => x.Username)
            .Select(x => new AdminMemberListItemDto(
                x.Id,
                x.Username,
                x.DiscordDisplayName,
                x.DiscordId,
                x.DiscordRank,
                x.CreatedAt,
                x.LastLogin,
                _db.MemberRoleAssignments
                    .Where(a => a.ProfileId == x.Id && a.RevokedAt == null)
                    .Select(a => a.AdminRole!.Name)
                    .ToArray()))
            .ToListAsync(cancellationToken);

        return profiles;
    }
}
