using System.Net.Http.Json;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace PackTracker.Infrastructure.Services;

public class ProfileService : IProfileService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;

    public ProfileService(AppDbContext db, IHttpClientFactory httpFactory, IConfiguration config)
    {
        _db = db;
        _httpFactory = httpFactory;
        _config = config;
    }

    public async Task<Profile?> UpsertFromDiscordAsync(string accessToken, string discordId, string username, string? avatarUrl)
    {
        var requiredGuildId = _config["Authentication:Discord:RequiredGuildId"];
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

    public Task<Profile?> GetByIdAsync(Guid id) =>
        _db.Profiles.SingleOrDefaultAsync(p => p.Id == id);

    public Task<Profile?> GetByDiscordIdAsync(string discordId) =>
        _db.Profiles.SingleOrDefaultAsync(p => p.DiscordId == discordId);
    
    public Task<Profile?> GetByNameAsync(string name) =>
        _db.Profiles.SingleOrDefaultAsync(p => p.Username.ToLower() == name.ToLower());

    public Task<IEnumerable<Profile>> GetAllAsync() =>
        Task.FromResult<IEnumerable<Profile>>(_db.Profiles.ToList());

    private class DiscordGuild
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
    }
}
