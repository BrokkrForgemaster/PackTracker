using PackTracker.Domain.Entities;

namespace PackTracker.Application.Interfaces;

/// <summary>
/// Contract for managing user profiles sourced from Discord authentication.
/// </summary>
public interface IProfileService
{
    Task<Profile?> UpsertFromDiscordAsync(string accessToken, string discordId, string username, string? avatarUrl,
        CancellationToken ct);
    Task<Profile?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Profile?> GetByDiscordIdAsync(string discordId, CancellationToken ct);
    
    Task<Profile?> GetByNameAsync(string name, CancellationToken ct);
    Task<IEnumerable<Profile>> GetAllAsync(CancellationToken ct);
}
