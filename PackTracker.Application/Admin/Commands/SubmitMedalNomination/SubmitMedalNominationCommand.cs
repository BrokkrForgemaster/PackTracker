using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Commands.SubmitMedalNomination;

public sealed record SubmitMedalNominationCommand(SubmitMedalNominationRequestDto Request) : IRequest<MedalNominationDto>;

public sealed class SubmitMedalNominationCommandHandler : IRequestHandler<SubmitMedalNominationCommand, MedalNominationDto>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditLogService _audit;
    private readonly IRbacService _rbac;

    public SubmitMedalNominationCommandHandler(
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

    public async Task<MedalNominationDto> Handle(SubmitMedalNominationCommand command, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.MedalsManage, cancellationToken);
        var ctx = await _rbac.GetCurrentAdminContextAsync(cancellationToken);

        var req = command.Request;
        var medal = await _db.MedalDefinitions.FirstOrDefaultAsync(m => m.Id == req.MedalDefinitionId, cancellationToken)
            ?? throw new InvalidOperationException("Medal definition not found.");

        var nomination = new MedalNomination
        {
            MedalDefinitionId = req.MedalDefinitionId,
            NomineeProfileId = req.NomineeProfileId,
            NomineeName = req.NomineeName.Trim(),
            NominatorName = ctx.DisplayName,
            Citation = req.Citation.Trim(),
            SubmittedAt = DateTime.UtcNow,
            Status = NominationStatus.Pending
        };

        _db.MedalNominations.Add(nomination);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(new AdminAuditLogEntryDto(
            "MedalNominationSubmitted", "MedalNomination", nomination.Id.ToString(),
            $"Nominated {nomination.NomineeName} for {medal.Name}.", "Info", null, null), cancellationToken);

        return ToDto(nomination, medal.Name, medal.ImagePath);
    }

    private static MedalNominationDto ToDto(MedalNomination n, string medalName, string? imagePath) => new(
        n.Id, n.MedalDefinitionId, medalName, imagePath,
        n.NomineeProfileId, n.NomineeName, n.NominatorName,
        n.Citation, n.Status.ToString(),
        n.SubmittedAt, n.ReviewedAt, n.ReviewedByName, n.ReviewNotes);
}
