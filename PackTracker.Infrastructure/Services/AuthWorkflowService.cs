using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Security;

namespace PackTracker.Infrastructure.Services;

public sealed class AuthWorkflowService : IAuthWorkflowService
{
    private readonly IProfileService _profiles;
    private readonly JwtTokenService _jwt;
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthWorkflowService> _logger;

    public AuthWorkflowService(
        IProfileService profiles,
        JwtTokenService jwt,
        AppDbContext db,
        IMemoryCache cache,
        ILogger<AuthWorkflowService> logger)
    {
        _profiles = profiles;
        _jwt = jwt;
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AuthCompletionResult> CompleteAsync(AuthCompletionRequest request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.UpsertFromDiscordAsync(
            request.AccessToken,
            request.DiscordId,
            request.Username,
            request.AvatarUrl,
            cancellationToken);

        if (profile is null)
        {
            _logger.LogWarning(
                "Discord-authenticated user {Username} ({DiscordId}) is not a member of the required guild.",
                request.Username,
                request.DiscordId);

            return new AuthCompletionResult(
                AuthCompletionStatus.AccessDenied,
                "Access Denied: You must be a member of the required Discord guild to use PackTracker.");
        }

        profile.DiscordDisplayName = request.DisplayName ?? profile.DiscordDisplayName;
        profile.Discriminator = request.Discriminator;
        await _db.SaveChangesAsync(cancellationToken);

        var (access, refresh, expires) = await _jwt.IssueTokenPairAsync(profile, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.ClientState))
        {
            _cache.Set(
                GetCacheKey(request.ClientState),
                new LoginTokenPayload(access, refresh, expires),
                TimeSpan.FromMinutes(5));

            _logger.LogInformation("Stored OAuth completion payload for client state {ClientState}.", request.ClientState);
        }
        else
        {
            _logger.LogWarning("OAuth completed without a client state; the desktop client cannot poll for tokens.");
        }

        return new AuthCompletionResult(AuthCompletionStatus.Success, "Success");
    }

    public Task<LoginTokenPayload?> PollAsync(string clientState, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_cache.TryGetValue<LoginTokenPayload>(GetCacheKey(clientState), out var payload))
        {
            _cache.Remove(GetCacheKey(clientState));
            return Task.FromResult<LoginTokenPayload?>(payload);
        }

        return Task.FromResult<LoginTokenPayload?>(null);
    }

    public async Task<AuthRefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var user = await _jwt.ValidateRefreshTokenAsync(refreshToken, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("Invalid refresh token attempt.");
            return new AuthRefreshResult(false, null, null, null, "Invalid or expired refresh token.");
        }

        var (access, refresh, expires) = await _jwt.IssueTokenPairAsync(user, cancellationToken);
        return new AuthRefreshResult(true, access, refresh, expires, "Success");
    }

    public Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
        => _jwt.RevokeRefreshTokenAsync(refreshToken, cancellationToken);

    private static string GetCacheKey(string state) => $"login-state:{state}";
}
