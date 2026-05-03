using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Enums;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Commands.DenyMedalNomination;

public sealed record DenyMedalNominationCommand(Guid NominationId, ReviewMedalNominationRequestDto Request) : IRequest<MedalNominationDto>;

public sealed class DenyMedalNominationCommandHandler : IRequestHandler<DenyMedalNominationCommand, MedalNominationDto>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditLogService _audit;
    private readonly IRbacService _rbac;

    public DenyMedalNominationCommandHandler(
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

    public async Task<MedalNominationDto> Handle(DenyMedalNominationCommand command, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.MedalsManage, cancellationToken);
        var ctx = await _rbac.GetCurrentAdminContextAsync(cancellationToken);

        var nomination = await _db.MedalNominations
            .Include(n => n.MedalDefinition)
            .FirstOrDefaultAsync(n => n.Id == command.NominationId, cancellationToken)
            ?? throw new InvalidOperationException("Nomination not found.");

        if (nomination.Status != NominationStatus.Pending)
            throw new InvalidOperationException("Nomination is not pending.");

        nomination.Status = NominationStatus.Denied;
        nomination.ReviewedAt = DateTime.UtcNow;
        nomination.ReviewedByName = ctx.DisplayName;
        nomination.ReviewNotes = command.Request.Notes?.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(new AdminAuditLogEntryDto(
            "MedalNominationDenied", "MedalNomination", nomination.Id.ToString(),
            $"Denied nomination of {nomination.NomineeName} for {nomination.MedalDefinition!.Name}.", "Info", null, null), cancellationToken);

        return new MedalNominationDto(
            nomination.Id, nomination.MedalDefinitionId, nomination.MedalDefinition!.Name, nomination.MedalDefinition.ImagePath,
            nomination.NomineeProfileId, nomination.NomineeName, nomination.NominatorName,
            nomination.Citation, nomination.Status.ToString(),
            nomination.SubmittedAt, nomination.ReviewedAt, nomination.ReviewedByName, nomination.ReviewNotes);
    }
}
