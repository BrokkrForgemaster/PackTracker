using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Queries.GetAdminRoles;

public sealed record GetAdminRolesQuery() : IRequest<IReadOnlyList<AdminRoleOptionDto>>;

public sealed class GetAdminRolesQueryHandler : IRequestHandler<GetAdminRolesQuery, IReadOnlyList<AdminRoleOptionDto>>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;

    public GetAdminRolesQueryHandler(IAdminDbContext db, IAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<AdminRoleOptionDto>> Handle(GetAdminRolesQuery request, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.MembersView, cancellationToken);

        return await _db.AdminRoles
            .AsNoTracking()
            .OrderBy(x => x.Tier)
            .ThenBy(x => x.Name)
            .Select(x => new AdminRoleOptionDto(x.Id, x.Name, x.Tier.ToString()))
            .ToListAsync(cancellationToken);
    }
}
