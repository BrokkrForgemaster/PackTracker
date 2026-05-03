using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Commands.AwardRibbon;

public sealed record AwardRibbonCommand(AwardRibbonRequestDto Request) : IRequest<AwardRibbonResultDto>;

public sealed class AwardRibbonCommandHandler : IRequestHandler<AwardRibbonCommand, AwardRibbonResultDto>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditLogService _audit;
    private readonly IRbacService _rbac;
    private readonly IDiscordAnnouncementService _discordAnnouncements;

    public AwardRibbonCommandHandler(
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

    public async Task<AwardRibbonResultDto> Handle(AwardRibbonCommand command, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.MedalsManage, cancellationToken);
        var ctx = await _rbac.GetCurrentAdminContextAsync(cancellationToken);

        var req = command.Request;
        var ribbonName = req.RibbonName.Trim();

        if (req.ProfileIds.Count == 0)
        {
            return new AwardRibbonResultDto(
                Guid.Empty,
                ribbonName,
                "No recipients selected",
                AlreadyAwarded: true);
        }

        var definition = await _db.MedalDefinitions
            .FirstOrDefaultAsync(
                m =>
                    string.Equals(m.Name, ribbonName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.AwardType, "Ribbon", StringComparison.OrdinalIgnoreCase),
                cancellationToken);

        if (definition is null)
        {
            definition = new MedalDefinition
            {
                Name = ribbonName,
                Description = req.RibbonDescription.Trim(),
                ImagePath = req.RibbonImagePath,
                PublicImageUrl = req.RibbonPublicImageUrl,
                SourceSystem = "PackTracker",
                DisplayOrder = 0,
                AwardType = "Ribbon",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.MedalDefinitions.Add(definition);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var profiles = await _db.Profiles
            .Where(x => req.ProfileIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var createdAwards = new List<(Guid AwardId, string RecipientName, string Citation)>();
        var skippedCount = 0;

        foreach (var profile in profiles)
        {
            var recipientName = !string.IsNullOrWhiteSpace(profile.DiscordDisplayName)
                ? profile.DiscordDisplayName
                : profile.Username;

            var existing = await _db.MedalAwards
                .FirstOrDefaultAsync(a =>
                    a.MedalDefinitionId == definition.Id &&
                    a.ProfileId == profile.Id &&
                    a.AwardType == "Ribbon",
                    cancellationToken);

            if (existing is not null)
            {
                skippedCount++;
                continue;
            }

            var award = new MedalAward
            {
                MedalDefinitionId = definition.Id,
                ProfileId = profile.Id,
                RecipientName = recipientName,
                Citation = string.IsNullOrWhiteSpace(req.Citation) ? null : req.Citation.Trim(),
                AwardedBy = ctx.DisplayName,
                AwardedAt = DateTime.UtcNow,
                ImportedAt = DateTime.UtcNow,
                SourceSystem = "PackTracker",
                AwardType = "Ribbon"
            };

            _db.MedalAwards.Add(award);

            createdAwards.Add((
                award.Id,
                recipientName,
                award.Citation ?? string.Empty));
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (createdAwards.Count > 0)
        {
            var recipientList = string.Join("\n", createdAwards.Select(x => $"• {x.RecipientName}"));

            await _discordAnnouncements.SendRibbonAwardedAsync(
                recipientList,
                ribbonName,
                string.IsNullOrWhiteSpace(req.Citation)
                    ? $"Awarded to:\n{recipientList}"
                    : req.Citation.Trim(),
                definition.PublicImageUrl,
                cancellationToken);
        }

        await _audit.WriteAsync(new AdminAuditLogEntryDto(
            "RibbonAwarded",
            "MedalAward",
            definition.Id.ToString(),
            $"Awarded '{ribbonName}' to {createdAwards.Count} member(s) by {ctx.DisplayName}. Skipped {skippedCount} duplicate(s).",
            "Info",
            null,
            null), cancellationToken);

        var firstAwardId = createdAwards.Count > 0
            ? createdAwards[0].AwardId
            : Guid.Empty;

        var resultRecipientName = createdAwards.Count == 1
            ? createdAwards[0].RecipientName
            : $"{createdAwards.Count} members";

        return new AwardRibbonResultDto(
            firstAwardId,
            ribbonName,
            resultRecipientName,
            AlreadyAwarded: createdAwards.Count == 0);
    }
}