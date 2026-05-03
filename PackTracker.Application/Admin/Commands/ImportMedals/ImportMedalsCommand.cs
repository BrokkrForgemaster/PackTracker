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
    private const string SourceSystem = "PackTracker";

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

    public async Task<ImportMedalsResultDto> Handle(
        ImportMedalsCommand command,
        CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(
            AdminPermissions.MedalsManage,
            cancellationToken);

        await _rbac.GetCurrentAdminContextAsync(cancellationToken);

        var result = await ImportAsync(command.Request, cancellationToken);

        await _audit.WriteAsync(
            new AdminAuditLogEntryDto(
                "AwardsImported",
                "AwardCatalog",
                SourceSystem,
                $"Imported awards from {SourceSystem}.",
                "Info",
                null,
                JsonSerializer.Serialize(result)),
            cancellationToken);

        return result;
    }

    private async Task<ImportMedalsResultDto> ImportAsync(
        ImportMedalsRequestDto request,
        CancellationToken cancellationToken)
    {
        var medalDefinitionsCreated = 0;
        var medalDefinitionsUpdated = 0;
        var awardsCreated = 0;
        var awardsSkipped = 0;

        var unmatchedRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unknownMedals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var existingDefinitions = (await _db.MedalDefinitions
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        ImportDefinitions(
            request.AvailableMedals,
            "Medal",
            existingDefinitions,
            ref medalDefinitionsCreated,
            ref medalDefinitionsUpdated,
            startDisplayOrder: 0);

        ImportDefinitions(
            request.AvailableRibbons,
            "Ribbon",
            existingDefinitions,
            ref medalDefinitionsCreated,
            ref medalDefinitionsUpdated,
            startDisplayOrder: request.AvailableMedals.Count);

        await _db.SaveChangesAsync(cancellationToken);

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

        foreach (var medalEntry in request.Recipients)
        {
            if (string.IsNullOrWhiteSpace(medalEntry.Key))
                continue;

            var medalName = medalEntry.Key.Trim();

            if (!existingDefinitions.TryGetValue(medalName, out var definition))
            {
                unknownMedals.Add(medalName);
                awardsSkipped += medalEntry.Value.Count;
                continue;
            }

            foreach (var recipientNameRaw in medalEntry.Value)
            {
                if (string.IsNullOrWhiteSpace(recipientNameRaw))
                    continue;

                var recipientName = recipientNameRaw.Trim();

                var profile = profileCandidates.FirstOrDefault(x =>
                    string.Equals(x.Username, recipientName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.DiscordDisplayName, recipientName, StringComparison.OrdinalIgnoreCase));

                if (profile is null)
                    unmatchedRecipients.Add(recipientName);

                var awardKey = BuildAwardKey(definition.Id, recipientName);

                if (!awardKeys.Add(awardKey))
                {
                    awardsSkipped++;
                    continue;
                }

                _db.MedalAwards.Add(new MedalAward
                {
                    MedalDefinitionId = definition.Id,
                    ProfileId = profile?.Id,
                    RecipientName = recipientName,
                    AwardType = definition.AwardType,
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

    private void ImportDefinitions(
        IReadOnlyList<ImportMedalDefinitionDto> incomingDefinitions,
        string awardType,
        Dictionary<string, MedalDefinition> existingDefinitions,
        ref int medalDefinitionsCreated,
        ref int medalDefinitionsUpdated,
        int startDisplayOrder)
    {
        for (var i = 0; i < incomingDefinitions.Count; i++)
        {
            var incoming = incomingDefinitions[i];

            if (string.IsNullOrWhiteSpace(incoming.Name))
                continue;

            var name = incoming.Name.Trim();
            var description = incoming.Description?.Trim() ?? string.Empty;
            var imagePath = string.IsNullOrWhiteSpace(incoming.Image)
                ? null
                : incoming.Image.Trim();
            var publicImageUrl = string.IsNullOrWhiteSpace(incoming.PublicUrl)
                ? null
                : incoming.PublicUrl.Trim();

            var displayOrder = startDisplayOrder + i;

            if (existingDefinitions.TryGetValue(name, out var definition))
            {
                definition.Description = description;
                definition.ImagePath = imagePath;
                definition.PublicImageUrl = publicImageUrl;
                definition.SourceSystem = SourceSystem;
                definition.DisplayOrder = displayOrder;
                definition.AwardType = awardType;
                definition.UpdatedAt = DateTime.UtcNow;

                medalDefinitionsUpdated++;
                continue;
            }

            definition = new MedalDefinition
            {
                Name = name,
                Description = description,
                ImagePath = imagePath,
                PublicImageUrl = publicImageUrl,
                SourceSystem = SourceSystem,
                DisplayOrder = displayOrder,
                AwardType = awardType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.MedalDefinitions.Add(definition);
            existingDefinitions.Add(name, definition);

            medalDefinitionsCreated++;
        }
    }

    private static string BuildAwardKey(Guid medalDefinitionId, string recipientName)
    {
        return $"{medalDefinitionId:N}:{recipientName.Trim()}";
    }
}