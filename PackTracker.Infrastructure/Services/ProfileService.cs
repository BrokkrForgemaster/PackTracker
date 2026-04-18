using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using PackTracker.Application.Options;
using PackTracker.Domain.Security;

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
    private readonly AuthOptions _authOptions;


    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileService"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="authOptions">The authentication options.</param>
    public ProfileService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ISettingsService settingsService,
        ILogger<ProfileService> logger,
        IOptions<AuthOptions> authOptions)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger;
        _authOptions = authOptions.Value;
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

        var requiredGuildId = _authOptions.Discord.RequiredGuildId;

        if (string.IsNullOrWhiteSpace(requiredGuildId))
        {
            throw new InvalidOperationException(
                "DiscordRequiredGuildId is not configured. Configure it before allowing Discord profile synchronization.");
        }

        _logger.LogInformation(
            "Starting Discord profile upsert. DiscordId={DiscordId} Username={Username}",
            discordId,
            username);
        var guilds = await GetUserGuildsAsync(accessToken, ct);

        if (guilds.Count == 0)
        {
            _logger.LogWarning(
                "Discord guild lookup returned no guilds for DiscordId={DiscordId}",
                discordId);
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
            _logger.LogInformation("Fetching Discord rank for GuildId={GuildId}", guildId);

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("Discord access token was empty.");
                return SecurityConstants.Roles.Foundling;
            }

            if (string.IsNullOrWhiteSpace(guildId))
            {
                _logger.LogWarning("GuildId was empty.");
                return SecurityConstants.Roles.Foundling;
            }

            // Step 1: Fetch the current user's member object.
            // Requires OAuth scope: guilds.members.read
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            using var memberResponse = await client.GetAsync(
                $"https://discord.com/api/users/@me/guilds/{guildId}/member",
                ct);

            if (!memberResponse.IsSuccessStatusCode)
            {
                var errorBody = await memberResponse.Content.ReadAsStringAsync(ct);

                _logger.LogWarning(
                    "Failed to fetch Discord member details. Status={StatusCode}, Reason={ReasonPhrase}, Error={Error}. " +
                    "This endpoint requires the guilds.members.read OAuth scope.",
                    (int)memberResponse.StatusCode,
                    memberResponse.ReasonPhrase,
                    errorBody);

                return SecurityConstants.Roles.Foundling;
            }

            var member = await memberResponse.Content.ReadFromJsonAsync<DiscordMember>(cancellationToken: ct);

            if (member == null)
            {
                _logger.LogWarning("Discord member response deserialized to null.");
                return SecurityConstants.Roles.Foundling;
            }

            if (member.Roles == null || member.Roles.Count == 0)
            {
                _logger.LogInformation("User has no roles in guild {GuildId}.", guildId);
                return SecurityConstants.Roles.Foundling;
            }

            _logger.LogInformation(
                "Found {Count} Discord role IDs for user in guild {GuildId}: {RoleIds}",
                member.Roles.Count,
                guildId,
                string.Join(", ", member.Roles));

            // Step 2: Fetch all guild roles using the bot token.
            // This lets us resolve names and hierarchy correctly.
            var botToken = _authOptions.Discord.BotToken;
            List<DiscordRole>? allRoles = null;

            if (!string.IsNullOrWhiteSpace(botToken))
            {
                var botClient = _httpClientFactory.CreateClient();
                botClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bot", botToken);

                using var rolesResponse = await botClient.GetAsync(
                    $"https://discord.com/api/guilds/{guildId}/roles",
                    ct);

                if (rolesResponse.IsSuccessStatusCode)
                {
                    allRoles = await rolesResponse.Content.ReadFromJsonAsync<List<DiscordRole>>(cancellationToken: ct);

                    _logger.LogInformation(
                        "Fetched {Count} guild roles for GuildId={GuildId}.",
                        allRoles?.Count ?? 0,
                        guildId);
                }
                else
                {
                    var rolesError = await rolesResponse.Content.ReadAsStringAsync(ct);

                    _logger.LogWarning(
                        "Failed to fetch guild roles via bot token. Status={StatusCode}, Reason={ReasonPhrase}, Error={Error}",
                        (int)rolesResponse.StatusCode,
                        rolesResponse.ReasonPhrase,
                        rolesError);
                }
            }
            else
            {
                _logger.LogWarning(
                    "No Discord bot token configured. Rank resolution will rely only on fallback role ID mapping.");
            }

            // Step 3: If we have guild roles, resolve the user's roles properly.
            if (allRoles is { Count: > 0 })
            {
                var userRoles = allRoles
                    .Where(r => member.Roles.Contains(r.Id))
                    .ToList();

                if (userRoles.Count > 0)
                {
                    _logger.LogInformation(
                        "Resolved {Count} guild role objects for the current user: {RoleNames}",
                        userRoles.Count,
                        string.Join(", ", userRoles.Select(r => r.Name)));

                    // A. Prefer your known organization hierarchy if available.
                    var highestKnownRole = ResolveHighestKnownRole(
                        userRoles.Select(r => r.Name).ToList());

                    if (!string.IsNullOrWhiteSpace(highestKnownRole))
                    {
                        _logger.LogInformation(
                            "Highest known PackTracker rank resolved via role names: {RoleName}",
                            highestKnownRole);

                        return highestKnownRole;
                    }

                    // B. If no known org role matches, fall back to actual Discord hierarchy.
                    // Assumes DiscordRole has a Position property.
                    var highestDiscordRole = userRoles
                        .OrderByDescending(r => r.Position)
                        .FirstOrDefault();

                    if (highestDiscordRole != null)
                    {
                        _logger.LogInformation(
                            "No known rank mapping matched. Highest Discord role by position is {RoleName} (Id={RoleId}, Position={Position}).",
                            highestDiscordRole.Name,
                            highestDiscordRole.Id,
                            highestDiscordRole.Position);

                        // If you do NOT want arbitrary Discord role names returned, remove this line
                        // and rely only on your fallback mapping below.
                        return highestDiscordRole.Name;
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "No guild role objects matched the member role IDs. Member role IDs: {RoleIds}",
                        string.Join(", ", member.Roles));
                }
            }

            // Step 4: Fallback to known static role ID mapping.
            var fallbackRole = ResolveHighestKnownRoleById(member.Roles);
            if (!string.IsNullOrWhiteSpace(fallbackRole))
            {
                _logger.LogInformation(
                    "Highest known PackTracker rank resolved via fallback role ID mapping: {RoleName}",
                    fallbackRole);

                return fallbackRole;
            }

            _logger.LogInformation(
                "No known rank could be resolved for guild {GuildId}. Returning default role {DefaultRole}.",
                guildId,
                SecurityConstants.Roles.Foundling);

            return SecurityConstants.Roles.Foundling;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Discord rank fetch was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Discord rank for GuildId={GuildId}.", guildId);
            return SecurityConstants.Roles.Foundling;
        }
    }

    /// <summary>
    /// Given a list of role names from Discord, returns the highest-ranked name
    /// that matches the known House Wolf hierarchy, or null if none match.
    /// </summary>
    private static string? ResolveHighestKnownRole(IEnumerable<string> roleNames)
    {
        string? highest = null;
        int highestPos = -1;

        foreach (var name in roleNames)
        {
            var pos = SecurityConstants.GetRolePosition(name);
            if (pos > highestPos)
            {
                highestPos = pos;
                highest = SecurityConstants.RoleHierarchy[pos];
            }
        }

        return highest;
    }

    /// <summary>
    /// Fallback: maps Discord role IDs to known role names, ordered highest-first.
    /// These IDs are specific to the House Wolf Discord guild.
    /// Update these if roles are recreated in Discord (IDs change on delete/recreate).
    /// </summary>
    private static string? ResolveHighestKnownRoleById(List<string> memberRoleIds)
    {
        // Ordered from highest rank to lowest so the first match wins.
        var knownRoles = new (string Id, string Name)[]
        {
            ("1119837295097434263", SecurityConstants.Roles.HandOfTheClan),
            ("1182799457650233465", SecurityConstants.Roles.HighCouncilor),
            ("1178165015694561290", SecurityConstants.Roles.Armor),
            ("1182796308516446288", SecurityConstants.Roles.FleetCommander),
            ("1442226431747948595", SecurityConstants.Roles.Captain),
            ("1318668583747715102", SecurityConstants.Roles.Lieutenant),
            ("1443066936337633400", SecurityConstants.Roles.Foundling),
            ("1357688449871646873", SecurityConstants.Roles.Foundling),
            ("1319052362261725214", SecurityConstants.Roles.Foundling)
        };

        foreach (var (id, name) in knownRoles)
        {
            if (memberRoleIds.Contains(id))
                return name;
        }

        return null;
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
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public sealed class DiscordMember
    {
        [JsonPropertyName("roles")]
        public List<string> Roles { get; set; } = new();
    }

    public sealed class DiscordRole
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("position")]
        public int Position { get; set; }
    }

    #endregion
}