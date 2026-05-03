using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Queries.GetMedalNominations;

public sealed record GetMedalNominationsQuery : IRequest<IReadOnlyList<MedalNominationDto>>;

public sealed class GetMedalNominationsQueryHandler : IRequestHandler<GetMedalNominationsQuery, IReadOnlyList<MedalNominationDto>>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;

    public GetMedalNominationsQueryHandler(IAdminDbContext db, IAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<MedalNominationDto>> Handle(GetMedalNominationsQuery _, CancellationToken ct)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.MedalsView, ct);

        return await _db.MedalNominations
            .Include(n => n.MedalDefinition)
            .OrderByDescending(n => n.SubmittedAt)
            .Select(n => new MedalNominationDto(
                n.Id, n.MedalDefinitionId, n.MedalDefinition!.Name, n.MedalDefinition.ImagePath,
                n.NomineeProfileId, n.NomineeName, n.NominatorName,
                n.Citation, n.Status.ToString(),
                n.SubmittedAt, n.ReviewedAt, n.ReviewedByName, n.ReviewNotes))
            .ToListAsync(ct);
    }
}
