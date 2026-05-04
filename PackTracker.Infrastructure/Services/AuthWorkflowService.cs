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
    private readonly IHouseWolfProfileService _houseWolf;
    private readonly JwtTokenService _jwt;
    private readonly AppDbContext _db;
    private readonly ILogger<AuthWorkflowService> _logger;

    public AuthWorkflowService(
        IProfileService profiles,
        IHouseWolfProfileService houseWolf,
        JwtTokenService jwt,
        AppDbContext db,
        ILogger<AuthWorkflowService> logger)
    {
        _profiles = profiles;
        _houseWolf = houseWolf;
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

        // --- HouseWolf Sync ---
        try
        {
            var hwProfile = await _houseWolf.GetProfileByDiscordIdAsync(request.DiscordId);
            if (hwProfile != null)
            {
                if (!string.IsNullOrWhiteSpace(hwProfile.ImageUrl))
                    profile.ShowcaseImageUrl = hwProfile.ImageUrl;

                if (!string.IsNullOrWhiteSpace(hwProfile.Bio))
                    profile.ShowcaseBio = hwProfile.Bio;

                if (!string.IsNullOrWhiteSpace(hwProfile.SubDivision))
                    profile.ShowcaseEyebrow = hwProfile.SubDivision;

                if (!string.IsNullOrWhiteSpace(hwProfile.Division))
                    profile.ShowcaseTagline = hwProfile.Division;

                if (!string.IsNullOrWhiteSpace(hwProfile.CharacterName))
                    profile.DiscordDisplayName = hwProfile.CharacterName;
                
                _logger.LogInformation("Profile {DiscordId} synced with HouseWolf data.", request.DiscordId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync profile {DiscordId} with HouseWolf during login.", request.DiscordId);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var (access, refresh, expires) = await _jwt.IssueTokenPairAsync(profile, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.ClientState))
        {
            // Upsert login state — if two OAuth callbacks race (browser retry on slow cold start),
            // the second insert would violate the unique index on ClientState.
            var existing = await _db.LoginStates
                .FirstOrDefaultAsync(s => s.ClientState == request.ClientState, cancellationToken);

            if (existing is null)
            {
                _db.LoginStates.Add(new LoginState
                {
                    ClientState = request.ClientState,
                    AccessToken = access,
                    RefreshToken = refresh,
                    ExpiresIn = expires
                });
            }
            else
            {
                existing.AccessToken = access;
                existing.RefreshToken = refresh;
                existing.ExpiresIn = expires;
            }

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
