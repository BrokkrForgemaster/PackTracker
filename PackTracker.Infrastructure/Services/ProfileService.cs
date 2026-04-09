using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// Provides profile persistence, retrieval, and Discord synchronization logic.
/// This service is responsible for validating required guild membership,
/// creating/updating user profiles, and preparing profile data for future
/// presence, role synchronization, and messaging features.
/// </summary>
public class ProfileService : IProfileService
{
    private readonly ILogger _logger;
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISettingsService _settingsService;
    

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileService"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="logger">The logger instance.</param>
    public ProfileService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ISettingsService settingsService,
        ILogger<ProfileService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger;
    }
    
    private static string? ResolveAvatar(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return null;

        return avatarUrl.Trim();
    }
        
    
    /// <summary>
    /// Creates or updates a profile using Discord-authenticated identity data.
    /// The user must belong to the configured required Discord guild.
    /// </summary>
    /// <param name="accessToken">The OAuth access token used to query Discord.</param>
    /// <param name="discordId">The Discord user identifier.</param>
    /// <param name="username">The Discord username.</param>
    /// <param name="avatarUrl">The Discord avatar URL, if available.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The created or updated profile if the user is valid and belongs to the required guild;
    /// otherwise <c>null</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the required Discord guild configuration is missing.
    /// </exception>
    public async Task<Profile?> UpsertFromDiscordAsync(
        string accessToken,
        string discordId,
        string username,
        string? avatarUrl,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("A Discord access token is required.", nameof(accessToken));

        if (string.IsNullOrWhiteSpace(discordId))
            throw new ArgumentException("A Discord user ID is required.", nameof(discordId));

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("A Discord username is required.", nameof(username));

        var settings = _settingsService.GetSettings();
        var requiredGuildId = settings.DiscordRequiredGuildId;

        if (string.IsNullOrWhiteSpace(requiredGuildId))
        {
            throw new InvalidOperationException(
                "DiscordRequiredGuildId is not configured. Configure it before allowing Discord profile synchronization.");
        }
        
        _logger.LogInformation(
            "Starting Discord profile upsert. DiscordId Username={Username}",
            username);
        var guilds = await GetUserGuildsAsync(accessToken, ct);

        if (guilds.Count == 0)
        {
            _logger.LogWarning(
                "Discord guild lookup returned no guilds for DiscordId");
        }

        var isMemberOfRequiredGuild = guilds.Any(g => g.Id == requiredGuildId);

        if (!isMemberOfRequiredGuild)
        {
            _logger.LogWarning(
                "Discord user rejected. DiscordId={DiscordId} Username={Username} RequiredGuildId={RequiredGuildId}",
                discordId,
                username,
                requiredGuildId);

            return null;
        }
        
        var resolvedAvatarUrl = ResolveAvatar(avatarUrl);
        
        // Fetch rank/role from Discord
        var highestRole = await GetDiscordRankAsync(accessToken, requiredGuildId, ct);

        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);

        if (profile == null)
        {
            profile = new Profile
            {
                DiscordId = discordId,
                Username = username,
                DiscordDisplayName = username,
                DiscordAvatarUrl = resolvedAvatarUrl,
                DiscordRank = highestRole,
                CreatedAt = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            };

            _db.Profiles.Add(profile);

            _logger.LogInformation(
                "Created new profile from Discord login. DiscordId={DiscordId} Username={Username} Rank={Rank}",
                discordId,
                username,
                highestRole);
        }
        else
        {
            profile.Username = username;

            profile.DiscordDisplayName = string.IsNullOrWhiteSpace(profile.DiscordDisplayName)
                ? username
                : profile.DiscordDisplayName;

            profile.DiscordAvatarUrl = resolvedAvatarUrl;
            profile.DiscordRank = highestRole;

            profile.LastLogin = DateTime.UtcNow;
            profile.LastSeenAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Updated existing profile from Discord login. ProfileId={ProfileId} DiscordId={DiscordId} Username={Username} Rank={Rank}",
                profile.Id,
                discordId,
                username,
                highestRole);
        }

        // Placeholder for future role/division sync:
        // - Leadership / Tacops / Specops / Locops / Arcops
        // - Discord display nickname
        // - Rank/role mapping
        // - DM preference defaults

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Discord profile upsert completed successfully. ProfileId={ProfileId} DiscordId={DiscordId}",
            profile.Id,
            discordId);

        return profile;
    }
    
    private async Task<string> GetDiscordRankAsync(string accessToken, string guildId, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("🔍 Fetching Discord rank for GuildId={GuildId}", guildId);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Fetch member details including roles
            var response = await client.GetAsync($"https://discord.com/api/users/@me/guilds/{guildId}/member", ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("⚠️ Failed to fetch Discord member details. Status: {Status}, Error: {Error}", response.StatusCode, errorBody);
                return "Member";
            }

            var member = await response.Content.ReadFromJsonAsync<DiscordMember>(ct);
            if (member == null || member.Roles.Count == 0)
            {
                _logger.LogInformation("ℹ️ User has no roles in the guild.");
                return "Member";
            }

            _logger.LogInformation("✅ Found {Count} roles for user: {RoleIds}", member.Roles.Count, string.Join(", ", member.Roles));

            // House Wolf specific role ID to Name mapping (Fallback)
            var houseWolfRoles = new Dictionary<string, string>
            {
                { "1119837295097434263", "Hand of the Clan" },
                { "1182799457650233465", "High Command" },
                { "1178165015694561290", "Officer" },
                { "1182796308516446288", "Veteran" },
                { "1442226431747948595", "Elite" },
                { "1318668583747715102", "Member" },
                { "1443066936337633400", "Recruit" },
                { "1357688449871646873", "Guest" },
                { "1182922578894008380", "Bot" },
                { "1319052362261725214", "Friend" }
            };

            // Fetch guild roles to determine hierarchy
            var rolesResponse = await client.GetAsync($"https://discord.com/api/guilds/{guildId}/roles", ct);
            if (!rolesResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("⚠️ Failed to fetch Discord guild roles. Status: {Status}. Using fallback mapping.", rolesResponse.StatusCode);
                
                // Use fallback mapping based on user's roles
                foreach (var mapping in houseWolfRoles)
                {
                    if (member.Roles.Contains(mapping.Key))
                    {
                        _logger.LogInformation("👑 Highest role identified via fallback: {RoleName}", mapping.Value);
                        return mapping.Value;
                    }
                }
                return "Member";
            }

            var allRoles = await rolesResponse.Content.ReadFromJsonAsync<List<DiscordRole>>(ct);
            if (allRoles == null)
            {
                _logger.LogWarning("⚠️ Could not deserialize guild roles.");
                return "Member";
            }

            // Find the highest role the user has based on position
            var userRoles = allRoles
                .Where(r => member.Roles.Contains(r.Id))
                .OrderByDescending(r => r.Position)
                .ToList();

            var highest = userRoles.FirstOrDefault();
            if (highest != null)
            {
                _logger.LogInformation("👑 Highest role identified: {RoleName} (Position {Position})", highest.Name, highest.Position);
                return highest.Name;
            }

            // If API didn't find anything but we have roles, try the fallback again
            foreach (var mapping in houseWolfRoles)
            {
                if (member.Roles.Contains(mapping.Key)) return mapping.Value;
            }

            return "Member";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching Discord rank.");
            return "Member";
        }
    }

    /// <summary>
    /// Gets a profile by its internal identifier.
    /// </summary>
    /// <param name="id">The profile identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching profile if found; otherwise <c>null</c>.</returns>
    public Task<Profile?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return _db.Profiles.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    /// <summary>
    /// Gets a profile by its Discord identifier.
    /// </summary>
    /// <param name="discordId">The Discord identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching profile if found; otherwise <c>null</c>.</returns>
    public Task<Profile?> GetByDiscordIdAsync(string discordId, CancellationToken ct)
    {
        return _db.Profiles.FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);
    }

    /// <summary>
    /// Gets a profile by username using a case-insensitive comparison.
    /// </summary>
    /// <param name="name">The username to search for.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching profile if found; otherwise <c>null</c>.</returns>
    public Task<Profile?> GetByNameAsync(string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult<Profile?>(null);

        return _db.Profiles.FirstOrDefaultAsync(
            p => p.Username.ToUpper() == name.ToUpper(),
            ct);
    }

    /// <summary>
    /// Gets all profiles.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A collection of all profiles.</returns>
    public async Task<IEnumerable<Profile>> GetAllAsync(CancellationToken ct)
    {
        return await _db.Profiles
            .AsNoTracking()
            .OrderBy(p => p.Username)
            .ToListAsync(ct);
    }

    #region Presence / Online Tracking

    /// <summary>
    /// Gets all profiles currently considered online.
    /// A user is considered online if they have been seen within the last 5 minutes.
    /// </summary>
    public async Task<IEnumerable<Profile>> GetOnlineAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);

        return await _db.Profiles
            .AsNoTracking()
            .Where(p => p.LastSeenAt >= cutoff)
            .OrderBy(p => p.Username)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Marks a user as online and updates their last seen timestamp.
    /// </summary>
    public async Task<Profile?> MarkOnlineAsync(string discordId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(discordId))
            return null;

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);

        if (profile == null)
        {
            _logger.LogWarning("MarkOnline failed. Profile not found for DiscordId={DiscordId}", discordId);
            return null;
        }

        profile.LastSeenAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("User marked online. DiscordId={DiscordId}", discordId);

        return profile;
    }

    /// <summary>
    /// Marks a user as offline by updating last seen timestamp.
    /// (We rely on time-based presence instead of a hard online flag.)
    /// </summary>
    public async Task<Profile?> MarkOfflineAsync(string discordId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(discordId))
            return null;

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);

        if (profile == null)
        {
            _logger.LogWarning("MarkOffline failed. Profile not found for DiscordId={DiscordId}", discordId);
            return null;
        }

        // Set last seen slightly in the past so they fall out of "online"
        profile.LastSeenAt = DateTime.UtcNow.AddMinutes(-10);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("User marked offline. DiscordId={DiscordId}", discordId);

        return profile;
    }

    /// <summary>
    /// Updates the user's last seen timestamp (heartbeat).
    /// This should be called periodically (SignalR ping, API call, etc).
    /// </summary>
    public async Task<Profile?> TouchLastSeenAsync(string discordId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(discordId))
            return null;

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);

        if (profile == null)
            return null;

        profile.LastSeenAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return profile;
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Gets the guilds available to the authenticated Discord user.
    /// </summary>
    /// <param name="accessToken">The Discord OAuth access token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of guilds returned by Discord.</returns>
    private async Task<List<DiscordGuild>> GetUserGuildsAsync(string accessToken, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            _logger.LogInformation("Requesting Discord guild membership list.");

            var guilds = await client.GetFromJsonAsync<List<DiscordGuild>>(
                "https://discord.com/api/users/@me/guilds",
                ct);

            var result = guilds ?? new List<DiscordGuild>();

            _logger.LogInformation(
                "Discord guild membership lookup completed. GuildCount={GuildCount}",
                result.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve Discord guild membership list.");
            throw;
        }
    }

    /// <summary>
    /// Normalizes a Discord avatar URL before persisting it.
    /// </summary>
    /// <param name="avatarUrl">The raw avatar URL.</param>
    /// <returns>A normalized avatar URL, or <c>null</c> if not supplied.</returns>
    private static string? NormalizeAvatarUrl(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return null;

        return avatarUrl.Trim();
    }

    #endregion

    #region Private Types

    /// <summary>
    /// Represents a minimal Discord guild payload returned by the Discord API.
    /// </summary>
    private sealed class DiscordGuild
    {
        /// <summary>
        /// Gets or sets the guild identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the guild name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }

    private sealed class DiscordMember
    {
        public List<string> Roles { get; set; } = new();
    }

    private sealed class DiscordRole
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Position { get; set; }
    }

    #endregion
}