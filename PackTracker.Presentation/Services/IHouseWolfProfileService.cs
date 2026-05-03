using System.Threading.Tasks;

namespace PackTracker.Presentation.Services;

public interface IHouseWolfProfileService
{
    Task<HouseWolfCharacterProfile?> GetProfileByDiscordIdAsync(string discordId);
    Task UpsertProfileAsync(HouseWolfCharacterProfile profile);
}

public class HouseWolfCharacterProfile
{
    public string? Id { get; set; }
    public string UserId { get; set; } = string.Empty; // Discord Id
    public string? CharacterName { get; set; }
    public string? Division { get; set; }
    public string? Bio { get; set; }
    public string? ImageUrl { get; set; }
    public string? SubDivision { get; set; }
}
