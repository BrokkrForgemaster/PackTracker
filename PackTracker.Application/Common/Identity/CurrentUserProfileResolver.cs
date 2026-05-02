using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;

namespace PackTracker.Application.Common.Identity;

public sealed class CurrentUserProfileResolver : ICurrentUserProfileResolver
{
    private readonly ICurrentUserService _currentUser;
    private readonly IProfileService _profileService;
    private readonly ILogger<CurrentUserProfileResolver> _logger;

    public CurrentUserProfileResolver(
        ICurrentUserService currentUser,
        IProfileService profileService,
        ILogger<CurrentUserProfileResolver> logger)
    {
        _currentUser = currentUser;
        _profileService = profileService;
        _logger = logger;
    }

    public async Task<CurrentUserProfileContext> ResolveAsync(CancellationToken cancellationToken)
    {
        var discordId = _currentUser.UserId;
        var profile = await _profileService.GetByDiscordIdAsync(discordId, cancellationToken);
        var profileId = profile?.Id;

        _logger.LogInformation(
            "[DIAGNOSTIC] Identity resolution: DiscordId={DiscordId}, ProfileId={ProfileId}",
            discordId,
            profileId?.ToString() ?? "NULL");

        if (profileId == null)
        {
            _logger.LogWarning(
                "[DIAGNOSTIC] Identity resolution failed for DiscordId={DiscordId}. Query may return incomplete results.",
                discordId);
        }

        return new CurrentUserProfileContext(discordId, profile);
    }
}
