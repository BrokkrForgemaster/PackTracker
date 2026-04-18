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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
