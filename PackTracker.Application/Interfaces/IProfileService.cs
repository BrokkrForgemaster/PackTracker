using PackTracker.Domain.Entities;

namespace PackTracker.Application.Interfaces;

/// <summary>
/// Contract for managing user profiles sourced from Discord authentication.
/// </summary>
public interface IProfileService
{
    Task<Profile?> UpsertFromDiscordAsync(string accessToken, string discordId, string username, string? avatarUrl);
    Task<Profile?> GetByIdAsync(Guid id);
    Task<Profile?> GetByDiscordIdAsync(string discordId);
    
    Task<Profile?> GetByNameAsync(string name);
    Task<IEnumerable<Profile>> GetAllAsync();
}
