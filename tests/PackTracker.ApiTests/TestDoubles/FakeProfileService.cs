using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.ApiTests.TestDoubles;

internal sealed class FakeProfileService : IProfileService
{
    private readonly AppDbContext _db;

    public FakeProfileService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Profile?> UpsertFromDiscordAsync(
        string accessToken,
        string discordId,
        string username,
        string? avatarUrl,
        CancellationToken ct)
    {
        var profile = await _db.Profiles.FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);
        if (profile != null)
        {
            profile.Username = username;
            if (!string.IsNullOrWhiteSpace(avatarUrl))
                profile.DiscordAvatarUrl = avatarUrl;

            await _db.SaveChangesAsync(ct);
            return profile;
        }

        profile = new Profile
        {
            DiscordId = discordId,
            Username = username,
            DiscordDisplayName = username,
            DiscordAvatarUrl = avatarUrl
        };

        _db.Profiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public Task<Profile?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Profiles.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<Profile?> GetByDiscordIdAsync(string discordId, CancellationToken ct) =>
        _db.Profiles.FirstOrDefaultAsync(x => x.DiscordId == discordId, ct);

    public Task<Profile?> GetByNameAsync(string name, CancellationToken ct) =>
        _db.Profiles.FirstOrDefaultAsync(x => x.Username == name, ct);

    public async Task<IEnumerable<Profile>> GetAllAsync(CancellationToken ct) =>
        await _db.Profiles.AsNoTracking().ToListAsync(ct);

    public async Task<IEnumerable<Profile>> GetOnlineAsync(CancellationToken ct) =>
        await _db.Profiles.AsNoTracking().ToListAsync(ct);

    public async Task<Profile?> MarkOnlineAsync(string discordId, CancellationToken ct)
    {
        var profile = await GetByDiscordIdAsync(discordId, ct);
        if (profile == null)
            return null;

        profile.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task<Profile?> MarkOfflineAsync(string discordId, CancellationToken ct)
    {
        var profile = await GetByDiscordIdAsync(discordId, ct);
        if (profile == null)
            return null;

        profile.LastSeenAt = DateTime.UtcNow.AddMinutes(-10);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task<Profile?> TouchLastSeenAsync(string discordId, CancellationToken ct)
    {
        var profile = await GetByDiscordIdAsync(discordId, ct);
        if (profile == null)
            return null;

        profile.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task<Profile?> UpdateShowcaseAsync(
        string discordId,
        string? showcaseImageUrl,
        string? showcaseEyebrow,
        string? showcaseTagline,
        string? showcaseBio,
        CancellationToken ct)
    {
        var profile = await GetByDiscordIdAsync(discordId, ct);
        if (profile == null)
            return null;

        profile.ShowcaseImageUrl = showcaseImageUrl;
        profile.ShowcaseEyebrow = showcaseEyebrow;
        profile.ShowcaseTagline = showcaseTagline;
        profile.ShowcaseBio = showcaseBio;
        await _db.SaveChangesAsync(ct);
        return profile;
    }
}
