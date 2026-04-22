using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Requests.Assistance;

internal static class AssistanceRequestAccess
{
    public static bool CanUseElevatedRequestActions(this ICurrentUserService currentUser, Profile profile) =>
        SecurityConstants.IsElevatedRequestRole(profile.DiscordRank)
        || SecurityConstants.ElevatedRequestRoles.Any(currentUser.IsInRole);

    public static bool CanManagePins(this ICurrentUserService currentUser, Profile profile) =>
        SecurityConstants.IsRallyMasterOrAbove(profile.DiscordRank)
        || SecurityConstants.RoleHierarchy
            .Where(SecurityConstants.IsRallyMasterOrAbove)
            .Any(currentUser.IsInRole);

    public static bool CanManage(this ICurrentUserService currentUser, Profile profile, AssistanceRequest request) =>
        profile.Id == request.CreatedByProfileId || currentUser.CanUseElevatedRequestActions(profile);
}
