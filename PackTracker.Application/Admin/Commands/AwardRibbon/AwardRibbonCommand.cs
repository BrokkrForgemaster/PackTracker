using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
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

    public AwardRibbonCommandHandler(
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

    public async Task<AwardRibbonResultDto> Handle(AwardRibbonCommand command, CancellationToken ct)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.MedalsManage, ct);
        var ctx = await _rbac.GetCurrentAdminContextAsync(ct);

        var req = command.Request;
        var ribbonName = req.RibbonName.Trim();
        var recipientName = req.RecipientName.Trim();

        // Find or create the MedalDefinition
        var definition = await _db.MedalDefinitions
            .FirstOrDefaultAsync(m => m.Name.ToLower() == ribbonName.ToLower(), ct);

        if (definition is null)
        {
            definition = new MedalDefinition
            {
                Name = ribbonName,
                Description = req.RibbonDescription.Trim(),
                ImagePath = req.RibbonImagePath,
                SourceSystem = "PackTracker",
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.MedalDefinitions.Add(definition);
            await _db.SaveChangesAsync(ct);
        }

        // Check for duplicate award
        var existing = await _db.MedalAwards
            .FirstOrDefaultAsync(a =>
                a.MedalDefinitionId == definition.Id &&
                a.RecipientName.ToLower() == recipientName.ToLower(), ct);

        if (existing is not null)
        {
            return new AwardRibbonResultDto(existing.Id, ribbonName, recipientName, AlreadyAwarded: true);
        }

        var award = new MedalAward
        {
            MedalDefinitionId = definition.Id,
            ProfileId = req.ProfileId,
            RecipientName = recipientName,
            Citation = string.IsNullOrWhiteSpace(req.Citation) ? null : req.Citation.Trim(),
            AwardedBy = ctx.DisplayName,
            AwardedAt = DateTime.UtcNow,
            ImportedAt = DateTime.UtcNow,
            SourceSystem = "PackTracker"
        };

        _db.MedalAwards.Add(award);
        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync(new AdminAuditLogEntryDto(
            "RibbonAwarded", "MedalAward", award.Id.ToString(),
            $"Awarded '{ribbonName}' to {recipientName} by {ctx.DisplayName}.",
            "Info", null, null), ct);

        return new AwardRibbonResultDto(award.Id, ribbonName, recipientName, AlreadyAwarded: false);
    }
}
