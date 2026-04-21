using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;

namespace PackTracker.Application.Blueprints.Queries.GetBlueprintEconomicSummary;

public sealed record GetBlueprintEconomicSummaryQuery(Guid BlueprintId) : IRequest<OperationResult<BlueprintEconomicSummaryDto>>;

public sealed class GetBlueprintEconomicSummaryQueryHandler : IRequestHandler<GetBlueprintEconomicSummaryQuery, OperationResult<BlueprintEconomicSummaryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetBlueprintEconomicSummaryQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<OperationResult<BlueprintEconomicSummaryDto>> Handle(GetBlueprintEconomicSummaryQuery request, CancellationToken cancellationToken)
    {
        var blueprint = await _db.Blueprints
            .Include(b => b.Recipe)
            .FirstOrDefaultAsync(b => b.Id == request.BlueprintId, cancellationToken);

        if (blueprint == null) return OperationResult<BlueprintEconomicSummaryDto>.Fail("Blueprint not found.");

        var recipe = blueprint.Recipe ?? await _db.BlueprintRecipes
            .FirstOrDefaultAsync(r => r.BlueprintId == blueprint.Id, cancellationToken);

        if (recipe == null) return OperationResult<BlueprintEconomicSummaryDto>.Fail("No recipe found for this blueprint.");

        var materials = await _db.BlueprintRecipeMaterials
            .Include(m => m.Material)
            .Where(m => m.BlueprintRecipeId == recipe.Id)
            .ToListAsync(cancellationToken);

        // Fetch all commodity names that match our materials for bulk price matching
        var materialNames = materials.Select(m => m.Material?.Name).Where(n => n != null).ToList();
        var commodityPrices = await _db.Commodities
            .AsNoTracking()
            .Where(c => materialNames.Contains(c.Name))
            .Select(c => new 
            { 
                c.Name, 
                AvgBuy = c.Prices.Select(p => p.PriceBuyAvg).FirstOrDefault() 
            })
            .ToListAsync(cancellationToken);

        // Also try to find a market price for the finished item itself
        var finishedItemCommodity = await _db.Commodities
            .AsNoTracking()
            .Where(c => c.Name == blueprint.CraftedItemName)
            .Select(c => new 
            { 
                AvgSell = c.Prices.Select(p => p.PriceSellAvg).FirstOrDefault() 
            })
            .FirstOrDefaultAsync(cancellationToken);

        var summary = new BlueprintEconomicSummaryDto
        {
            BlueprintId = blueprint.Id,
            ItemName = blueprint.CraftedItemName ?? "Unknown Item",
            EstimatedMarketValue = finishedItemCommodity != null ? (decimal)finishedItemCommodity.AvgSell : null,
            Materials = new List<MaterialCostDto>()
        };

        foreach (var m in materials)
        {
            var price = commodityPrices.FirstOrDefault(p => p.Name == m.Material?.Name);
            var avgPrice = price != null ? (decimal)price.AvgBuy : 0;

            summary.Materials.Add(new MaterialCostDto
            {
                MaterialId = m.MaterialId,
                MaterialName = m.Material?.Name ?? "Unknown Material",
                QuantityRequired = m.QuantityRequired,
                Unit = m.Unit,
                AvgPricePerUnit = avgPrice
            });
        }

        summary.TotalCraftingCost = summary.Materials.Sum(m => m.TotalCost);

        return OperationResult<BlueprintEconomicSummaryDto>.Ok(summary);
    }
}
