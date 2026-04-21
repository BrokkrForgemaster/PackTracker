using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Security;

namespace PackTracker.Infrastructure.Services;

public sealed class AuthWorkflowService : IAuthWorkflowService
{
    private readonly IProfileService _profiles;
    private readonly JwtTokenService _jwt;
    private readonly AppDbContext _db;
    private readonly ILogger<AuthWorkflowService> _logger;

    public AuthWorkflowService(
        IProfileService profiles,
        JwtTokenService jwt,
        AppDbContext db,
        ILogger<AuthWorkflowService> logger)
    {
        _profiles = profiles;
        _jwt = jwt;
        _db = db;
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
            // PERSIST TO DATABASE for multi-instance support
            var state = new LoginState
            {
                ClientState = request.ClientState,
                AccessToken = access,
                RefreshToken = refresh,
                ExpiresIn = expires
            };

            _db.LoginStates.Add(state);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Stored OAuth completion payload for client state {ClientState}.", request.ClientState);
        }
        else
        {
            _logger.LogWarning("OAuth completed without a client state; the desktop client cannot poll for tokens.");
        }

        return new AuthCompletionResult(AuthCompletionStatus.Success, "Success");
    }

    public async Task<LoginTokenPayload?> PollAsync(string clientState, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientState)) return null;

        var state = await _db.LoginStates
            .FirstOrDefaultAsync(s => s.ClientState == clientState, cancellationToken);

        if (state == null) return null;

        if (state.IsExpired)
        {
            _logger.LogWarning("Login state for {ClientState} has expired.", clientState);
            _db.LoginStates.Remove(state);
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }

        var payload = new LoginTokenPayload(state.AccessToken, state.RefreshToken, state.ExpiresIn);

        // Remove after successful poll (One-time use)
        _db.LoginStates.Remove(state);
        await _db.SaveChangesAsync(cancellationToken);

        return payload;
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
}
