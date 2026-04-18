using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Crafting;

internal static class CraftingRequestAccess
{
    public static bool CanManage(this ICurrentUserService currentUser, Profile profile, Guid? creatorProfileId) =>
        creatorProfileId.HasValue && profile.Id == creatorProfileId.Value
        || SecurityConstants.IsElevatedRequestRole(profile.DiscordRank)
        || SecurityConstants.ElevatedRequestRoles.Any(currentUser.IsInRole);
}
