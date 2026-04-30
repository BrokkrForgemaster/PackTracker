using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Queries.GetAdminMedals;

public sealed record GetAdminMedalsQuery() : IRequest<AdminMedalsDto>;

public sealed class GetAdminMedalsQueryHandler : IRequestHandler<GetAdminMedalsQuery, AdminMedalsDto>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;

    public GetAdminMedalsQueryHandler(IAdminDbContext db, IAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<AdminMedalsDto> Handle(GetAdminMedalsQuery request, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.MedalsView, cancellationToken);

        var medals = await _db.MedalDefinitions
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => new AdminMedalDefinitionDto(
                x.Id,
                x.Name,
                x.Description,
                x.ImagePath,
                x.SourceSystem,
                x.DisplayOrder,
                x.Awards.Count))
            .ToListAsync(cancellationToken);

        var awards = await _db.MedalAwards
            .AsNoTracking()
            .OrderByDescending(x => x.AwardedAt ?? x.ImportedAt)
            .ThenBy(x => x.RecipientName)
            .Select(x => new AdminMedalAwardDto(
                x.Id,
                x.MedalDefinitionId,
                x.MedalDefinition.Name,
                x.ProfileId,
                x.RecipientName,
                x.Profile != null ? (x.Profile.DiscordDisplayName ?? x.Profile.Username) : null,
                x.AwardedAt,
                x.ImportedAt,
                x.SourceSystem))
            .ToListAsync(cancellationToken);

        return new AdminMedalsDto(medals, awards);
    }
}
