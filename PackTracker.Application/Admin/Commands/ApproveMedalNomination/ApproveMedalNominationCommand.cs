using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Commands.ApproveMedalNomination;

public sealed record ApproveMedalNominationCommand(Guid NominationId, ReviewMedalNominationRequestDto Request) : IRequest<MedalNominationDto>;

public sealed class ApproveMedalNominationCommandHandler : IRequestHandler<ApproveMedalNominationCommand, MedalNominationDto>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditLogService _audit;
    private readonly IRbacService _rbac;
    private readonly IDiscordAnnouncementService _discordAnnouncements;

    public ApproveMedalNominationCommandHandler(
        IAdminDbContext db,
        IAuthorizationService authorization,
        IAuditLogService audit,
        IRbacService rbac,
        IDiscordAnnouncementService discordAnnouncements)
    {
        _db = db;
        _authorization = authorization;
        _audit = audit;
        _rbac = rbac;
        _discordAnnouncements = discordAnnouncements;
    }

    public async Task<MedalNominationDto> Handle(ApproveMedalNominationCommand command, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.MedalsManage, cancellationToken);
        var ctx = await _rbac.GetCurrentAdminContextAsync(cancellationToken);

        var nomination = await _db.MedalNominations
            .Include(n => n.MedalDefinition)
            .FirstOrDefaultAsync(n => n.Id == command.NominationId, cancellationToken)
            ?? throw new InvalidOperationException("Nomination not found.");

        if (nomination.Status != NominationStatus.Pending)
            throw new InvalidOperationException("Nomination is not pending.");

        nomination.Status = NominationStatus.Approved;
        nomination.ReviewedAt = DateTime.UtcNow;
        nomination.ReviewedByName = ctx.DisplayName;
        nomination.ReviewNotes = command.Request.Notes?.Trim();

        _db.MedalAwards.Add(new MedalAward
        {
            MedalDefinitionId = nomination.MedalDefinitionId,
            ProfileId = nomination.NomineeProfileId,
            RecipientName = nomination.NomineeName,
            AwardedAt = DateTime.UtcNow,
            ImportedAt = DateTime.UtcNow,
            SourceSystem = "PackTracker",
            Citation = nomination.Citation,
            AwardedBy = ctx.DisplayName
        });

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _discordAnnouncements.SendRibbonAwardedAsync(
                nomination.NomineeName,
                nomination.MedalDefinition!.Name,
                nomination.Citation,
                nomination.MedalDefinition.PublicImageUrl,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't fail the command if the announcement fails, but log it
            await _audit.WriteAsync(new AdminAuditLogEntryDto(
                "DiscordAnnouncementFailed", "MedalNomination", nomination.Id.ToString(),
                $"Failed to send Discord announcement for {nomination.NomineeName}: {ex.Message}", "Warning", null, null), cancellationToken);
        }

        await _audit.WriteAsync(new AdminAuditLogEntryDto(
            "MedalNominationApproved", "MedalNomination", nomination.Id.ToString(),
            $"Approved nomination of {nomination.NomineeName} for {nomination.MedalDefinition!.Name}.", "Info", null, null), cancellationToken);

        return new MedalNominationDto(
            nomination.Id, nomination.MedalDefinitionId, nomination.MedalDefinition!.Name, nomination.MedalDefinition.ImagePath,
            nomination.NomineeProfileId, nomination.NomineeName, nomination.NominatorName,
            nomination.Citation, nomination.Status.ToString(),
            nomination.SubmittedAt, nomination.ReviewedAt, nomination.ReviewedByName, nomination.ReviewNotes);
    }
}
