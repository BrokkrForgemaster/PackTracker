using Microsoft.EntityFrameworkCore;
using PackTracker.Domain.Entities;

namespace PackTracker.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Profile> Profiles { get; }
    DbSet<AssistanceRequest> AssistanceRequests { get; }
    DbSet<CraftingRequest> CraftingRequests { get; }
    DbSet<MaterialProcurementRequest> MaterialProcurementRequests { get; }
    DbSet<GuideRequest> GuideRequests { get; }
    DbSet<RequestTicket> RequestTickets { get; }
    DbSet<Blueprint> Blueprints { get; }
    DbSet<MemberBlueprintOwnership> MemberBlueprintOwnerships { get; }
    DbSet<BlueprintRecipe> BlueprintRecipes { get; }
    DbSet<BlueprintRecipeMaterial> BlueprintRecipeMaterials { get; }
    DbSet<RequestComment> RequestComments { get; }
    DbSet<Material> Materials { get; }
    DbSet<OrgInventoryItem> OrgInventoryItems { get; }
    DbSet<Commodity> Commodities { get; }
    DbSet<CommodityPrice> CommodityPrices { get; }
    DbSet<RequestClaim> RequestClaims { get; }
    DbSet<LobbyChatMessage> LobbyChatMessages { get; }
    DbSet<MedalDefinition> MedalDefinitions { get; }
    DbSet<MedalAward> MedalAwards { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> ExecuteSqlInterpolatedAsync(FormattableString sql, CancellationToken cancellationToken = default);
}
