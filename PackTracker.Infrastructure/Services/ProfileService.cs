using System.Net.Http.Json;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PackTracker.Infrastructure.Services;

public class ProfileService : IProfileService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ISettingsService _settingsService;

    public ProfileService(AppDbContext db, IHttpClientFactory httpFactory, ISettingsService settingsService)
    {
        _db = db;
        _httpFactory = httpFactory;
        _settingsService = settingsService;
    }

    public async Task<Profile?> UpsertFromDiscordAsync(string accessToken, string discordId, string username,
        string? avatarUrl, CancellationToken ct)
    {
        var settings = _settingsService.GetSettings();
        var requiredGuildId = settings.DiscordRequiredGuildId;
        if (string.IsNullOrEmpty(requiredGuildId))
            throw new InvalidOperationException("RequiredGuildId is not configured");

        // 🔑 Call Discord API for guild membership
        var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var guilds = await client.GetFromJsonAsync<List<DiscordGuild>>("https://discord.com/api/users/@me/guilds");

        if (guilds?.Any(g => g.Id == requiredGuildId) != true)
        {
            // ❌ User is not in House Wolf
            return null;
        }

        // ✅ Upsert into database
        var profile = await _db.Profiles.SingleOrDefaultAsync(p => p.DiscordId == discordId);
        if (profile is null)
        {
            profile = new Profile
            {
                Id = Guid.NewGuid(),
                DiscordId = discordId,
                Username = username,
                AvatarUrl = avatarUrl,
                CreatedAt = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow
            };
            _db.Profiles.Add(profile);
        }
        else
        {
            profile.Username = username;
            profile.AvatarUrl = avatarUrl;
            profile.LastLogin = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return profile;
    }

    public Task<Profile?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Profiles.SingleOrDefaultAsync(p => p.Id == id);

    public Task<Profile?> GetByDiscordIdAsync(string discordId, CancellationToken ct) =>
        _db.Profiles.SingleOrDefaultAsync(p => p.DiscordId == discordId);
    
    public Task<Profile?> GetByNameAsync(string name, CancellationToken ct) =>
        _db.Profiles.SingleOrDefaultAsync(p => p.Username.ToLower() == name.ToLower());

    public Task<IEnumerable<Profile>> GetAllAsync(CancellationToken ct) =>
        Task.FromResult<IEnumerable<Profile>>(_db.Profiles.ToList());

    private class DiscordGuild
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
    }
}
