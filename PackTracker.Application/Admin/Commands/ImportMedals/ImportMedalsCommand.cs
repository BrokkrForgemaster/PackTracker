using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Commands.ImportMedals;

public sealed record ImportMedalsCommand(ImportMedalsRequestDto Request) : IRequest<ImportMedalsResultDto>;

public sealed class ImportMedalsCommandHandler : IRequestHandler<ImportMedalsCommand, ImportMedalsResultDto>
{
    private const string SourceSystem = "wolf_raid";

    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditLogService _audit;
    private readonly IRbacService _rbac;

    public ImportMedalsCommandHandler(
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

    public async Task<ImportMedalsResultDto> Handle(ImportMedalsCommand command, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.MedalsManage, cancellationToken);

        await _rbac.GetCurrentAdminContextAsync(cancellationToken);
        var result = await ImportAsync(command.Request, cancellationToken);

        await _audit.WriteAsync(
            new AdminAuditLogEntryDto(
                "MedalsImported",
                "MedalCatalog",
                SourceSystem,
                $"Imported medals from {SourceSystem}.",
                "Info",
                null,
                JsonSerializer.Serialize(result)),
            cancellationToken);

        return result;
    }

    private async Task<ImportMedalsResultDto> ImportAsync(ImportMedalsRequestDto request, CancellationToken cancellationToken)
    {
        var medalDefinitionsCreated = 0;
        var medalDefinitionsUpdated = 0;
        var awardsCreated = 0;
        var awardsSkipped = 0;

        var unmatchedRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unknownMedals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var existingMedals = (await _db.MedalDefinitions.ToListAsync(cancellationToken))
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < request.AvailableMedals.Count; i++)
        {
            var incoming = request.AvailableMedals[i];
            if (string.IsNullOrWhiteSpace(incoming.Name))
            {
                continue;
            }

            var name = incoming.Name.Trim();
            if (existingMedals.TryGetValue(name, out var medal))
            {
                medal.Description = incoming.Description.Trim();
                medal.ImagePath = string.IsNullOrWhiteSpace(incoming.Image) ? null : incoming.Image.Trim();
                medal.SourceSystem = SourceSystem;
                medal.DisplayOrder = i;
                medal.UpdatedAt = DateTime.UtcNow;
                medalDefinitionsUpdated++;
                continue;
            }

            medal = new MedalDefinition
            {
                Name = name,
                Description = incoming.Description.Trim(),
                ImagePath = string.IsNullOrWhiteSpace(incoming.Image) ? null : incoming.Image.Trim(),
                SourceSystem = SourceSystem,
                DisplayOrder = i,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.MedalDefinitions.Add(medal);
            existingMedals.Add(name, medal);
            medalDefinitionsCreated++;
        }

        var existingAwards = await _db.MedalAwards
            .AsNoTracking()
            .Select(x => new { x.MedalDefinitionId, x.RecipientName })
            .ToListAsync(cancellationToken);

        var awardKeys = existingAwards
            .Select(x => BuildAwardKey(x.MedalDefinitionId, x.RecipientName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var profileCandidates = await _db.Profiles
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.Username,
                x.DiscordDisplayName
            })
            .ToListAsync(cancellationToken);

        foreach (var recipient in request.Recipients)
        {
            var recipientName = recipient.Key.Trim();
            if (string.IsNullOrWhiteSpace(recipientName))
            {
                continue;
            }

            var profile = profileCandidates.FirstOrDefault(x =>
                string.Equals(x.Username, recipientName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.DiscordDisplayName, recipientName, StringComparison.OrdinalIgnoreCase));

            if (profile is null)
            {
                unmatchedRecipients.Add(recipientName);
            }

            foreach (var medalNameRaw in recipient.Value)
            {
                if (string.IsNullOrWhiteSpace(medalNameRaw))
                {
                    continue;
                }

                var medalName = medalNameRaw.Trim();
                if (!existingMedals.TryGetValue(medalName, out var medal))
                {
                    unknownMedals.Add(medalName);
                    awardsSkipped++;
                    continue;
                }

                var awardKey = BuildAwardKey(medal.Id, recipientName);
                if (!awardKeys.Add(awardKey))
                {
                    awardsSkipped++;
                    continue;
                }

                _db.MedalAwards.Add(new MedalAward
                {
                    MedalDefinitionId = medal.Id,
                    ProfileId = profile?.Id,
                    RecipientName = recipientName,
                    ImportedAt = DateTime.UtcNow,
                    SourceSystem = SourceSystem
                });

                awardsCreated++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new ImportMedalsResultDto(
            medalDefinitionsCreated,
            medalDefinitionsUpdated,
            awardsCreated,
            awardsSkipped,
            unmatchedRecipients.OrderBy(static x => x).ToArray(),
            unknownMedals.OrderBy(static x => x).ToArray());
    }

    private static string BuildAwardKey(Guid medalDefinitionId, string recipientName) =>
        $"{medalDefinitionId:N}:{recipientName.Trim()}";
}
