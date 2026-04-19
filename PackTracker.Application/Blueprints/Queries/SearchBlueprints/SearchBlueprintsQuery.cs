using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Blueprints.Queries.SearchBlueprints;

public sealed record SearchBlueprintsQuery(string? Query, string? Category, bool InGameOnly) : IRequest<IReadOnlyList<BlueprintSearchItemDto>>;

public sealed class SearchBlueprintsQueryHandler : IRequestHandler<SearchBlueprintsQuery, IReadOnlyList<BlueprintSearchItemDto>>
{
    private readonly IApplicationDbContext _db;

    public SearchBlueprintsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<BlueprintSearchItemDto>> Handle(SearchBlueprintsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Domain.Entities.Blueprint> blueprintQuery = _db.Blueprints.AsNoTracking();

        if (request.InGameOnly)
            blueprintQuery = blueprintQuery.Where(x => x.IsInGameAvailable);

        if (!string.IsNullOrWhiteSpace(request.Category))
            blueprintQuery = blueprintQuery.Where(x => x.Category == request.Category);

        var blueprints = await blueprintQuery
            .OrderBy(x => x.BlueprintName)
            .Take(200)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = request.Query.Trim();
            blueprints = blueprints
                .Where(x =>
                    (!string.IsNullOrWhiteSpace(x.BlueprintName) && x.BlueprintName.Contains(term, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(x.CraftedItemName) && x.CraftedItemName.Contains(term, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(x.Category) && x.Category.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var blueprintIds = blueprints.Select(x => x.Id).ToList();

        var ownerCounts = await _db.MemberBlueprintOwnerships
            .AsNoTracking()
            .Where(x => blueprintIds.Contains(x.BlueprintId) && x.InterestType == MemberBlueprintInterestType.Owns)
            .GroupBy(x => x.BlueprintId)
            .Select(group => new { BlueprintId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.BlueprintId, x => x.Count, cancellationToken);

        return blueprints
            .Select(x => new BlueprintSearchItemDto
            {
                Id = x.Id,
                WikiUuid = Guid.TryParse(x.WikiUuid, out var wikiUuid) ? wikiUuid : x.Id,
                BlueprintName = x.BlueprintName,
                CraftedItemName = x.CraftedItemName,
                Category = x.Category,
                IsInGameAvailable = x.IsInGameAvailable,
                AcquisitionSummary = x.AcquisitionSummary,
                DataConfidence = x.DataConfidence,
                VerifiedOwnerCount = ownerCounts.TryGetValue(x.Id, out var count) ? count : 0
            })
            .Take(100)
            .ToList();
    }
}
